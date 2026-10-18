# 贡献指南

感谢你愿意改进“验证码剪贴板”！

## 提交 Issue

- **报告问题时请写明：** Windows 版本、应用版本、复现步骤，以及处理记录里对应条目的状态和详情。
- **请勿贴出真实的验证码、短信全文、手机号或账号信息。** 需要举例时，把数字换成虚构值，例如 `1065900000000000001`、`123456`。
- 安全漏洞不要公开提交，请按 [SECURITY.md](SECURITY.md) 私下报告。

## 提交代码

1. Fork 仓库，从 `main` 拉出分支，例如 `fix/clipboard-retry`、`feat/sms-keywords`。
2. 按 [docs/development.md](docs/development.md) 准备环境。提交前确认以下两条命令都通过：

   ```powershell
   dotnet test tests\OtpClipboard.Core.Tests
   dotnet build src\OtpClipboard\OtpClipboard.csproj -c Release -p:Platform=x64
   ```

3. 修改验证码识别规则或其他 `OtpClipboard.Core` 中的逻辑时，必须补充测试用例。
4. 改动界面、托盘或浮窗时，请在 PR 中说明手动验证的步骤，最好附上截图（截图里同样不要出现真实验证码）。
5. 一个 PR 只做一件事。用户能感知到的改动，请同时更新 [CHANGELOG.md](CHANGELOG.md) 的“未发布”部分。

## 提交信息

用中文或英文都可以。第一行简要说明改了什么，不超过 50 字；需要时空一行再写原因和细节。例如：

```text
修复剪贴板被占用时的重试间隔

部分输入法会短暂锁定剪贴板，原先 80ms 内重试三次仍可能失败。
```

## 代码约定

详见 [docs/development.md](docs/development.md#代码约定)。最重要的几条：

- 遵循 `.editorconfig`。
- 不依赖界面的逻辑放进 `OtpClipboard.Core`。
- 不新增联网、遥测或读取通知以外数据的功能。这个工具的前提就是完全在本机处理。

## 许可

提交代码即表示你同意以 [MIT 许可证](LICENSE) 发布你的贡献。
