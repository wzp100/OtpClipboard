param()

$ErrorActionPreference = 'Stop'
[Console]::OutputEncoding = [System.Text.UTF8Encoding]::new($false)
$OutputEncoding = [Console]::OutputEncoding
$root = [System.IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))
Push-Location -LiteralPath $root
try {
    # 同时检查已跟踪文件和未忽略的新文件；不输出匹配内容，防止密钥进入终端日志。
    $paths = @(git -c core.quotepath=false ls-files --cached --others --exclude-standard)
    if ($LASTEXITCODE -ne 0) { throw '无法枚举 Git 文件。' }
    $violations = [System.Collections.Generic.List[string]]::new()
    $patterns = @(
        '-----BEGIN (?:RSA |EC |OPENSSH |DSA )?PRIVATE KEY-----',
        'gh[pousr]_[A-Za-z0-9]{30,}',
        'github_pat_[A-Za-z0-9_]{30,}',
        'sk-(?:proj-)?[A-Za-z0-9_-]{24,}',
        'AKIA[A-Z0-9]{16}',
        '(?i)[A-Z]:[\\/]Users[\\/][^\\/\s<>"'']+',
        '(?i)(?:api[_-]?key|client[_-]?secret|password)\s*[:=]\s*["''][A-Za-z0-9_+/=-]{16,}["'']'
    )
    foreach ($relative in $paths) {
        if ($relative -match '(^|/)(output|dist|\.local|bin|obj)/|(^|/)(notification-history|preferences)\.json|\.(pfx|p12|key|pem|snk|log|binlog|dmp|dump)$|(^|/)\.env($|\.)') {
            $violations.Add("禁止发布的本机或敏感文件：$relative")
            continue
        }
        if ([System.IO.Path]::GetExtension($relative) -notin @('.cs', '.csproj', '.props', '.targets', '.xml', '.xaml', '.appxmanifest', '.manifest', '.pubxml', '.json', '.yml', '.yaml', '.md', '.ps1', '.py', '.cmd', '.txt', '.iss', '.svg')) { continue }
        $fullPath = Join-Path $root $relative
        if (-not (Test-Path -LiteralPath $fullPath -PathType Leaf)) { continue }
        $content = (Get-Content -LiteralPath $fullPath -Encoding utf8) -join "`n"
        foreach ($pattern in $patterns) {
            if ([regex]::IsMatch($content, $pattern)) {
                $violations.Add("疑似凭据或个人绝对路径：$relative")
                break
            }
        }
    }
    if ($violations.Count -gt 0) {
        $violations | ForEach-Object { Write-Host $_ }
        throw "隐私检查未通过：$($violations.Count) 个文件需要检查。"
    }
    Write-Host "隐私检查通过：$($paths.Count) 个候选文件；图片需另行人工检查。"
}
finally { Pop-Location }
