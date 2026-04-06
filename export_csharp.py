from ultralytics import YOLO

if __name__ == '__main__':
    model = YOLO("./runs/detect/mahjong_model/exp134/weights/best.pt")  # 模型路径
    model.export(format='onnx', imgsz=1280) # 导出 ONNX 模型，保持输入尺寸一致