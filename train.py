from ultralytics import YOLO

if __name__ == '__main__':
     # YOLOv8n (Nano)   最快、最轻量。特别适合算力有限的边缘设备或移动端，以及追求极致速度的实时应用
     # YOLOv8s (Small)  速度与精度的平衡点。在绝大多数通用场景下都有不错的表现，是工业部署的常见选择
     # YOLOv8m (Medium) 更高的精度。适合对准确性有更高要求，且算力相对充足的服务器端任务
     # YOLOv8l (Large)  精度优先。适用于复杂环境下的精细检测，如小目标或高难度任务，但对算力要求也更高
     # YOLOv8x (XLarge) 极致精度。面向对精度有顶级要求的科研或高端工业应用，需要非常强大的GPU算力支持

    model = YOLO('yolov8n.pt') 

    # 训练模型
    model.train(
        # --- 核心参数 ---
        data='mahjong_dataset/dataset.yaml',
        imgsz=1280,     # 改为 1280 或 640，根据识别准确率调整
        batch=12,        # 增大 imgsz 后需要减小 batch

         # --- 显存优化 ---
        amp=True,       # 16位混合精度训练，能加速训练并节省显存，适合支持的GPU
        workers=20,     # 数据加载线程数，根据CPU核心数调整，过多可能导致系统不稳定

        # --- 置信度与精度优化 ---
        # batch=2-4时，AdamW收敛更快，在小批量上优势明显
        # batch=8-16时，AdamW、SGD均可
        # batch>16时，SGD通常能获得更高的最终精度
        # optimizer='AdamW',             # 小批量训练时的稳定之选
        # lr0=0.003,                     # AdamW 优化的初始学习率
        # lrf=0.12,                      # 最终学习率 = lr0 * lrf
        # warmup_epochs=3,               # 学习率预热轮数
        # cls=1.0,                       # 关键优化：提升分类损失权重
        #box=7.5,                       # 边界框损失权重，保持默认
        #dfl=1.5,                       # 分布焦点损失权重，保持默认
        # 极致精度时启用
        optimizer='SGD',      # SGD优化器，追求极致精度
        lr0=0.01,             # SGD的标准初始学习率，对应batch=8-16
        weight_decay=0.0005,  # SGD对应的权重衰减
        cls=1.0,              # 将分类损失权重提高到1.0

        # --- 数据增强 ---
        close_mosaic=15,               # 最后15个epoch关闭mosaic增强
        mixup=0.0,                     # 关闭 mixup 增强
        copy_paste=0.0,                # 关闭 copy_paste 增强

        # --- 训练控制 ---
        device=0,                       # GPU编号，CPU则用 'cpu'
        epochs=300,                     # 训练轮数，根据训练曲线调整，过拟合时可减少
        patience=50,                    # 早停机制，连续50轮没有提升就停止训练
        save=True,                      # 保存训练checkpoints和最佳模型
        project='mahjong_model',        # 可选：指定保存目录
        name='exp1'                     # 可选：指定本次实验名称 
    )

    # 验证模型
    model.val(split='test')