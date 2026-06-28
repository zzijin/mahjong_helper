"""
导出训练好的 YOLO 模型到 ONNX（FP32 + FP16），供 TileMind C# 端 ONNX Runtime 推理。
同时复制训练指标 (results.csv) 和训练参数 (args.yaml) 到目标文件夹。
"""

import shutil
import torch
from pathlib import Path
from ultralytics import YOLO

# =========================================================================
# 配置
# =========================================================================
EXPORTS = [
    {
        "name": "yolov8n-v4",
        "pt_path": "runs/detect/mahjong_model/v8-exp4/weights/best.pt",
        "results_src": "runs/detect/mahjong_model/v8-exp4/results.csv",
        "args_src": "runs/detect/mahjong_model/v8-exp4/args.yaml",
        "mAP50": 0.995,
        "mAP50_95": 0.949,
    },
    {
        "name": "yolov8n-v3",
        "pt_path": "runs/detect/mahjong_model/v8-exp3/weights/best.pt",
        "results_src": "runs/detect/mahjong_model/v8-exp3/results.csv",
        "args_src": "runs/detect/mahjong_model/v8-exp3/args.yaml",
        "mAP50": 0.993,
        "mAP50_95": 0.930,
    },
    {
        "name": "yolo26n-v5",
        "pt_path": "runs/detect/mahjong_model/yolo26-exp5/weights/best.pt",
        "results_src": "runs/detect/mahjong_model/yolo26-exp5/results.csv",
        "args_src": "runs/detect/mahjong_model/yolo26-exp5/args.yaml",
        "mAP50": 0.991,
        "mAP50_95": 0.932,
    },
]

OUTPUT_BASE = Path("mahjong_model")
EXPORT_KWARGS = dict(
    format="onnx",
    dynamic=True,
    imgsz=1280,
    simplify=True,
    opset=17,
    device=0,
)

if __name__ == "__main__":
    # 检查 FP16 支持
    supports_fp16 = False
    if torch.cuda.is_available():
        cc = torch.cuda.get_device_capability()
        supports_fp16 = cc[0] > 7 or (cc[0] == 7 and cc[1] >= 0)
        print(f"GPU: {torch.cuda.get_device_name(0)} (CC {cc[0]}.{cc[1]})")
        print(f"FP16 supported: {supports_fp16}")
    else:
        print("CUDA not available, FP16 disabled")

    for exp in EXPORTS:
        print()
        print("=" * 60)
        print(f"Exporting {exp['name']}")
        print(f"  mAP50={exp['mAP50']}, mAP50-95={exp['mAP50_95']}")
        print("=" * 60)

        out_dir = OUTPUT_BASE / exp["name"]
        out_dir.mkdir(parents=True, exist_ok=True)

        model = YOLO(exp["pt_path"])

        # --- FP32 ---
        print("  Exporting FP32...")
        fp32_path = model.export(**EXPORT_KWARGS, half=False)
        fp32_dst = out_dir / f"{exp['name'].split('-')[0]}-fp32.onnx"
        shutil.copy(fp32_path, fp32_dst)
        size_mb = fp32_dst.stat().st_size / 1024 / 1024
        print(f"    -> {fp32_dst} ({size_mb:.1f} MB)")

        # --- FP16 ---
        if supports_fp16:
            print("  Exporting FP16...")
            fp16_path = model.export(**EXPORT_KWARGS, half=True)
            fp16_dst = out_dir / f"{exp['name'].split('-')[0]}-fp16.onnx"
            shutil.copy(fp16_path, fp16_dst)
            size_mb = fp16_dst.stat().st_size / 1024 / 1024
            print(f"    -> {fp16_dst} ({size_mb:.1f} MB)")

        # --- 复制训练指标 ---
        results_src = Path(exp["results_src"])
        if results_src.exists():
            shutil.copy(results_src, out_dir / "results.csv")
            print(f"    copied results.csv")

        # --- 复制训练参数 ---
        args_src = Path(exp["args_src"])
        if args_src.exists():
            shutil.copy(args_src, out_dir / "args.yaml")
            print(f"    copied args.yaml")

    print()
    print("Done!")
