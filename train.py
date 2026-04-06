from ultralytics import YOLO

if __name__ == '__main__':
     # YOLOv8n (Nano)   最快、最轻量。特别适合算力有限的边缘设备或移动端，以及追求极致速度的实时应用
     # YOLOv8s (Small)  速度与精度的平衡点。在绝大多数通用场景下都有不错的表现，是工业部署的常见选择
     # YOLOv8m (Medium) 更高的精度。适合对准确性有更高要求，且算力相对充足的服务器端任务
     # YOLOv8l (Large)  精度优先。适用于复杂环境下的精细检测，如小目标或高难度任务，但对算力要求也更高
     # YOLOv8x (XLarge) 极致精度。面向对精度有顶级要求的科研或高端工业应用，需要非常强大的GPU算力支持

    model = YOLO('yolov8m.pt') 
    model.train(
        data='mahjong_dataset/dataset.yaml',
        epochs=100,
        imgsz=1280,     # 改为 1280 或 640，根据识别准确率调整
        batch=8,      # 增大 imgsz 后需要减小 batch（4080 12G 用 1280 batch=8 较稳妥）
        device=0,      # GPU编号，CPU则用 'cpu'
        project='mahjong_model',
        name='exp1'
    )