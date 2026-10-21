# 宣传图生成记录

使用 Codex 内置图像生成工具；生成的 PNG 保存到 `docs/media/`。配图为概念插画，不是真实软件截图。所有验证码均为虚构示例。

## 封面提示词

Use case: ads-marketing. Asset type: 小红书竖版封面，2:3比例，精致中文效率工具海报。Primary request: 宣传真实项目“验证码剪贴板 OtpClipboard”，它在Windows上读取微软手机连接转发到通知中心的验证码并复制到本机剪贴板。设计干净现代、奶油白纸感底色、电光蓝和薄荷绿点缀，大字号黑色中文，留白有节奏，高级独立开发产品视觉。画面中间放抽象的手机与Windows电脑等距立体插画，短信卡片示例写“验证码 246810”，通过流动蓝色箭头连接到电脑剪贴板卡片“已复制 246810”，旁边清晰标注“虚构示例”。不要做成真实软件截图。文案须逐字准确且醒目：顶部小标签“iPhone × Windows”；主标题分三行“验证码来了”“电脑直接”“Ctrl + V”；副标题“验证码剪贴板 · OtpClipboard”；底部胶囊标签“手机连接转发 / 本机识别 / 自动复制”；底部小字“需完成手机连接配对，并授权通知访问”。仅这些文字，避免虚构App Store标志、认证、下载量、速度数据，避免品牌logo，非真实产品界面，中文清楚且无乱码，无水印。

## 说明图提示词

Use case: infographic-diagram. Asset type: 小红书竖版第二张说明配图，2:3比例。为“验证码剪贴板 OtpClipboard”制作简洁可读的上手与隐私卡片，独立开发产品视觉，奶油白背景，大黑字、电光蓝、薄荷绿，轻微立体图标，留白充分，有清晰信息层级。不要用真实软件截图，不要出现真实通知或个人资料。顶部小字“验证码剪贴板 · OtpClipboard”；大标题“3 步，少抄一次验证码”。中部三个纵向编号卡片，准确中文文案：“01  配对手机连接”“确认手机通知进入 Windows 通知中心”；“02  授权通知访问”“打开工具，允许读取通知”；“03  收到后直接粘贴”“识别验证码 → 自动复制 → Ctrl + V”。下半部薄荷绿隐私区，大标题“在本机处理”；三个短句：“验证码不写入应用历史文件”“重启只保留状态和时间”“关闭 Windows 剪贴板历史与同步”。底部蓝色小卡片“示例验证码 246810 · 虚构演示”，最后一行小字“通知转发依赖手机连接；特殊输入框请手动粘贴”。这些中文与英文逐字准确，文字大而清楚，专业信息设计，不增加绝对安全承诺，不虚构下载量认证或速度指标，无水印。


### 说明图修订提示词

仅将第三列“不加入 Windows 剪贴板历史与同步”用于准确表达单次写入选项，保持其余布局与虚构演示信息。最终文件为 `docs/media/xiaohongshu-guide-v2.png`。

## v1.0.5 说明图

Use case: infographic-diagram. Asset type: 小红书竖版第二张说明配图，2:3 比例。Primary request: 为真实开源项目“验证码剪贴板 OtpClipboard”制作 v1.0.5 更新说明图。Scene/background: 温暖奶油白纸张质感，顶部和底部留足留白，电光蓝与薄荷绿作为强调色。Subject: 中间用四个清晰卡片表达主要变化：“关闭界面，后台继续监听”“识别本机完成，不联网”“验证码不写入应用历史”“再次打开，界面按需启动”。顶部文案只能是“v1.0.5 更轻一点”；副标题只能是“验证码剪贴板 · OtpClipboard”。Use small abstract Windows laptop, tray icon, notification bell and clipboard illustrations, not photorealistic device brand logos. Bottom exact text: “示例验证码 246810 · 虚构演示” and “需手机连接配对并授权通知访问；特殊输入框可手动 Ctrl + V”。Style/medium: clean premium Chinese editorial infographic, precise information hierarchy, crisp Chinese typography, legible at phone screen size. Constraints: render all Chinese and English text verbatim; no additional claims; no authentication marks; no download counts; no performance guarantee; no watermark; no real SMS; no personal data; no real app screenshot; avoid tacky neon, oversaturation and clutter. Final file is `docs/media/xiaohongshu-guide-v3.png`.

## 生成状态

最终配图必须逐张检查文字准确性、虚构示例和隐私内容。若本机没有 `OPENAI_API_KEY`，不得跳过校验或把未审阅的图片当作发布素材。
