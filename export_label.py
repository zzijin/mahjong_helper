from ultralytics import YOLO

if __name__ == '__main__':
    model = YOLO("./runs/detect/mahjong_model/exp138/weights/best.pt")  # 模型路径
    model.export(format="onnx", imgsz=1280, opset=12, device=0)  # opset=12 是关键