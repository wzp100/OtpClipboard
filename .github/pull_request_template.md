## 改了什么

<!-- 简要说明改动和原因。关联 Issue 请写 Fixes #编号。 -->

## 如何验证

- [ ] `dotnet test tests\OtpClipboard.Core.Tests` 通过
- [ ] `dotnet build src\OtpClipboard\OtpClipboard.csproj -c Release -p:Platform=x64` 通过
- [ ] 涉及界面、托盘或浮窗时，已手动验证（步骤或截图写在下面）

## 检查

- [ ] 改动识别规则时补充了测试
- [ ] 用户能感知的改动已更新 CHANGELOG.md 的“未发布”部分
- [ ] 代码、测试、截图中没有真实的验证码、短信、手机号或个人路径
