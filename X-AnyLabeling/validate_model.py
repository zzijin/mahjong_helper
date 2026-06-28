"""
使用 ONNX 模型对 X-AnyLabeling 标注图片进行推理，
对比识别结果与 JSON 标注文件的差异。

检查逻辑：对每张图片运行模型推理，将预测框与 JSON 标注框按 IoU 匹配。
同位置（IoU > 阈值）但 label 不一致的记录为异常。

用法：
  python validate_model.py                     # 使用默认配置
  python validate_model.py --model yolov8n-best.onnx  # 指定模型
  python validate_model.py --iou 0.3 --conf 0.5        # 调整匹配参数
"""

import argparse
import json
from pathlib import Path
from collections import defaultdict

from ultralytics import YOLO

# =========================================================================
# 配置
# =========================================================================
BASE = Path(__file__).parent
MODELS_DIR = BASE / "models"
DATASET_DIR = BASE / "dataset"
CHECK_DIRS = ["label-01", "label-02", "label-03"]

# 37 类标签名（0-indexed，与 YOLO 训练一致）
CLASS_NAMES = [
    "1m", "2m", "3m", "4m", "5m", "6m", "7m", "8m", "9m", "0m",
    "1p", "2p", "3p", "4p", "5p", "6p", "7p", "8p", "9p", "0p",
    "1s", "2s", "3s", "4s", "5s", "6s", "7s", "8s", "9s", "0s",
    "1z", "2z", "3z", "4z", "5z", "6z", "7z",
]

DEFAULT_MODEL = "yolov8n.onnx"
IOU_THRESHOLD = 0.3      # 预测框与标注框的 IoU 匹配阈值
CONF_THRESHOLD = 0.25    # 模型置信度阈值
IMGSZ = 1280


def compute_iou(box1, box2):
    """计算两个 [x1,y1,x2,y2] 框的 IoU"""
    x1 = max(box1[0], box2[0])
    y1 = max(box1[1], box2[1])
    x2 = min(box1[2], box2[2])
    y2 = min(box1[3], box2[3])
    inter = max(0, x2 - x1) * max(0, y2 - y1)
    area1 = (box1[2] - box1[0]) * (box1[3] - box1[1])
    area2 = (box2[2] - box2[0]) * (box2[3] - box2[1])
    union = area1 + area2 - inter
    return inter / union if union > 0 else 0


def main():
    parser = argparse.ArgumentParser(description="校验 ONNX 模型识别结果与 JSON 标注的一致性")
    parser.add_argument("--model", default=DEFAULT_MODEL, help=f"模型文件名 (位于 {MODELS_DIR})")
    parser.add_argument("--iou", type=float, default=IOU_THRESHOLD, help="匹配 IoU 阈值")
    parser.add_argument("--conf", type=float, default=CONF_THRESHOLD, help="置信度阈值")
    args = parser.parse_args()

    model_path = MODELS_DIR / args.model
    if not model_path.exists():
        # 允许绝对路径
        model_path = Path(args.model)
    if not model_path.exists():
        print(f"Error: model not found: {args.model}")
        return

    print(f"Model: {model_path}")
    print(f"IoU threshold: {args.iou}, Conf threshold: {args.conf}")
    print()

    # 使用 ultralytics API 加载模型（自动处理 ONNX 后处理）
    model = YOLO(str(model_path))

    # 统计数据
    total_images = 0
    total_gt_boxes = 0
    total_pred_boxes = 0
    matched_pairs = 0
    label_mismatches = []
    unmatched_gt = []
    unmatched_pred = []
    errors_by_label = defaultdict(int)
    file_errors = defaultdict(lambda: {"mismatch": 0, "missed": 0, "total": 0})

    for d in CHECK_DIRS:
        dir_path = DATASET_DIR / d
        if not dir_path.exists():
            print(f"Skip: {d} (not found)")
            continue

        json_files = sorted(dir_path.glob("*.json"))
        print(f"{d}: {len(json_files)} images")

        for json_file in json_files:
            # 加载 JSON 标注
            with open(json_file, "r", encoding="utf-8") as f:
                data = json.load(f)

            # 查找对应图片
            img_path = json_file.with_suffix(".png")
            if not img_path.exists():
                img_path = json_file.with_suffix(".jpg")
            if not img_path.exists():
                continue

            total_images += 1

            # 用 ultralytics API 推理（自动 NMS + 后处理）
            results = model(str(img_path), imgsz=IMGSZ, conf=args.conf, device="cpu", verbose=False)
            detections = []  # [[x1,y1,x2,y2,conf,cls_id], ...]
            for r in results:
                boxes = r.boxes
                if boxes is not None:
                    for j in range(len(boxes)):
                        xyxy = boxes.xyxy[j].tolist()
                        conf = float(boxes.conf[j])
                        cls_id = int(boxes.cls[j])
                        detections.append([xyxy[0], xyxy[1], xyxy[2], xyxy[3], conf, cls_id])
            total_pred_boxes += len(detections)

            # 收集 JSON 标注框
            gt_boxes = []  # [[x1,y1,x2,y2], label_name]
            for shape in data.get("shapes", []):
                pts = shape.get("points", [])
                label = shape.get("label", "")
                if not label:
                    continue
                if len(pts) == 4:
                    xs = [p[0] for p in pts]
                    ys = [p[1] for p in pts]
                    gt_boxes.append([min(xs), min(ys), max(xs), max(ys), label])
                elif len(pts) == 2:
                    gt_boxes.append([
                        min(pts[0][0], pts[1][0]), min(pts[0][1], pts[1][1]),
                        max(pts[0][0], pts[1][0]), max(pts[0][1], pts[1][1]),
                        label,
                    ])
            total_gt_boxes += len(gt_boxes)

            # 匹配
            gt_matched = [False] * len(gt_boxes)
            pred_matched = [False] * len(detections)

            for pi, pred in enumerate(detections):
                pred_label = CLASS_NAMES[int(pred[5])] if 0 <= int(pred[5]) < 37 else f"cls{int(pred[5])}"
                best_iou, best_gi = 0, -1
                for gi, gt in enumerate(gt_boxes):
                    if gt_matched[gi]:
                        continue
                    iou = compute_iou(pred[:4], gt[:4])
                    if iou > best_iou:
                        best_iou, best_gi = iou, gi

                if best_iou >= args.iou and best_gi >= 0:
                    pred_matched[pi] = True
                    gt_matched[best_gi] = True
                    matched_pairs += 1
                    gt_label = gt_boxes[best_gi][4]

                    if pred_label != gt_label:
                        label_mismatches.append(
                            f"{d}/{json_file.name}: pred={pred_label}(conf={pred[4]:.2f}) vs gt={gt_label}"
                        )
                        errors_by_label[f"{gt_label}->{pred_label}"] += 1
                        file_errors[f"{d}/{json_file.stem}"]["mismatch"] += 1
                        file_errors[f"{d}/{json_file.stem}"]["total"] += 1

            # 未匹配的标注框
            for gi, gt in enumerate(gt_boxes):
                if not gt_matched[gi]:
                    unmatched_gt.append(f"{d}/{json_file.name}: gt={gt[4]}")
                    file_errors[f"{d}/{json_file.stem}"]["missed"] += 1
                    file_errors[f"{d}/{json_file.stem}"]["total"] += 1

            # 未匹配的预测框
            for pi, pred in enumerate(detections):
                if not pred_matched[pi]:
                    cls_id = int(pred[5])
                    pred_label = CLASS_NAMES[cls_id] if 0 <= cls_id < 37 else f"cls{cls_id}"
                    unmatched_pred.append(
                        f"{d}/{json_file.name}: pred={pred_label}(conf={pred[4]:.2f})"
                    )

    # =========================================================================
    # 输出结果
    # =========================================================================
    print()
    print("=" * 60)
    print("Summary")
    print("=" * 60)
    print(f"  Images checked:    {total_images}")
    print(f"  GT boxes:          {total_gt_boxes}")
    print(f"  Pred boxes:        {total_pred_boxes}")
    print(f"  Matched pairs:     {matched_pairs}")
    print(f"  Label mismatches:  {len(label_mismatches)}")
    print(f"  Unmatched GT:      {len(unmatched_gt)}")
    print(f"  Unmatched Pred:    {len(unmatched_pred)}")

    # Label mismatches
    print()
    print("=" * 60)
    print(f"Label Mismatches ({len(label_mismatches)})")
    print("=" * 60)
    if label_mismatches:
        for item in label_mismatches[:50]:
            print(f"  {item}")
        if len(label_mismatches) > 50:
            print(f"  ... and {len(label_mismatches) - 50} more")

    # Error type breakdown
    if errors_by_label:
        print()
        print("Error types (gt -> pred):")
        for err_type, count in sorted(errors_by_label.items(), key=lambda x: -x[1])[:20]:
            print(f"  {err_type}: {count}")

    # Unmatched GT
    print()
    print("=" * 60)
    print(f"Unmatched GT boxes (missed by model, {len(unmatched_gt)})")
    print("=" * 60)
    if unmatched_gt:
        for item in unmatched_gt[:20]:
            print(f"  {item}")
        if len(unmatched_gt) > 20:
            print(f"  ... and {len(unmatched_gt) - 20} more")

    # Unmatched Pred
    print()
    print("=" * 60)
    print(f"Unmatched Pred boxes (possible FP, {len(unmatched_pred)})")
    print("=" * 60)
    if unmatched_pred:
        for item in unmatched_pred[:20]:
            print(f"  {item}")
        if len(unmatched_pred) > 20:
            print(f"  ... and {len(unmatched_pred) - 20} more")

    # Top 20 files by errors
    print()
    print("=" * 60)
    print("Top 20 files by errors (mismatch + missed GT)")
    print("=" * 60)
    sorted_files = sorted(file_errors.items(), key=lambda x: -x[1]["total"])
    for rank, (fname, errs) in enumerate(sorted_files[:20], 1):
        print(f"  {rank:>2}. {fname}: {errs['total']:>3} errors (mismatch={errs['mismatch']}, missed={errs['missed']})")
    if len(sorted_files) > 20:
        remaining = sum(e['total'] for _, e in sorted_files[20:])
        print(f"  ... +{len(sorted_files) - 20} more files ({remaining} errors)")

    # Per-directory summary
    print()
    print("Errors by directory:")
    for d in CHECK_DIRS:
        d_files = [(k, v) for k, v in file_errors.items() if k.startswith(d + "/")]
        d_total = sum(v['total'] for _, v in d_files)
        print(f"  {d}: {len(d_files)} files with errors, {d_total} total errors")

    # Final verdict
    print()
    error_rate = len(label_mismatches) / matched_pairs * 100 if matched_pairs > 0 else 0
    print(f"Label accuracy: {100 - error_rate:.1f}% ({matched_pairs - len(label_mismatches)}/{matched_pairs})")
    if label_mismatches:
        print(f"WARNING: {len(label_mismatches)} label mismatches found!")
    else:
        print("OK: No label mismatches.")


if __name__ == "__main__":
    main()
