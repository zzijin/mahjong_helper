import torch
from ultralytics import YOLO

if __name__ == '__main__':
    model = YOLO("./mahjong_model/exp146-n/weights/best.pt")  # 模型路径

    supports_fp16 = False
    # 检查 CUDA 是否可用
    if torch.cuda.is_available():
        # 获取当前设备的计算能力 (Compute Capability)
        # 计算能力是 NVIDIA 为不同架构 GPU 设定的版本号，是判断特性的核心依据
        cc = torch.cuda.get_device_capability()
        # 计算能力大于等于 7.0 的 GPU，通常都具备高效的 FP16 加速能力
        supports_fp16 = cc[0] > 7 or (cc[0] == 7 and cc[1] >= 0)
        if supports_fp16:
            print(f"✅ 设备支持高效的 FP16 推理! (计算能力: {cc})")
        else:
            print(f"⚠️ 设备对 FP16 支持有限，建议使用 FP32 以确保稳定性。 (计算能力: {cc})")
    else:
        print("❌ 未检测到 CUDA 设备，无法使用 GPU 推理。")
    
    model.export(
        format="onnx",          # 导出格式，支持 'onnx', 'torchscript', 'openvino', 'coreml', 'tflite' 等多种格式，选择适合部署环境的格式
        dynamic=True,           # 决定模型是否能接受动态变化的输入。设为 True 可大幅提高部署灵活性，能处理不同尺寸的图像。若设为 False (默认值)，输入尺寸则被固定为imgsz参数的值
        imgsz=1280,             # 当dynamic=False时，此值固定为输入图像的尺寸。如果使用了dynamic=True，此参数主要影响导出时的默认或最大尺寸
        simplify=True,          # 强烈建议开启。它会优化模型的计算图，移除冗余操作，从而减小模型体积并提升推理速度，且不影响精度
        opset=17,               # ONNX算子集版本。选择较高的版本能支持更多新特性，建议使用 17 或更高版本。需注意，过低版本可能不支持某些操作导致导出失败，过高版本在非常老的推理引擎上可能不兼容；X-AnyLabeling 需要 opset=12
        half=supports_fp16,     # 是否启用FP16半精度。建议推理环境支持时开启。这可以减小模型体积（约一半），并利用支持FP16的硬件（如部分GPU）加速推理
        #int8=False,            # 是否启用INT8量化。追求极致性能时可开启。它能进一步压缩模型，带来显著的推理加速，尤其适合边缘设备，但可能会带来微小的精度损失
        device=0                # 导出设备，通常为GPU编号（如0），如果没有GPU则使用'cpu'。使用GPU导出通常更快，尤其是当模型较大时
        ) 