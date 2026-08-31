using System.Globalization;
using Monica.Guide;

namespace Monica.Guide.App;

/// <summary>Per-run setup UI state: language, wizard inputs, and the latest previewed plan.</summary>
public sealed class SetupSession
{
    private const string Chinese = "zh-CN";
    private const string English = "en-US";

    public static readonly IReadOnlyList<string> Languages = [Chinese, English];

    public SetupSession()
    {
        Language = CultureInfo.CurrentUICulture.Name.StartsWith("zh", StringComparison.OrdinalIgnoreCase)
            ? Chinese
            : English;
    }

    public string Language { get; private set; }

    public event Action? LanguageChanged;

    /// <summary>
    /// The product this wizard session operates on. Choosing a bundle of another registered
    /// product switches the session so plan, port, update, and integration surfaces all
    /// follow one definition.
    /// </summary>
    public AgentProductDefinition CurrentProduct { get; private set; } = KnownAgentProducts.Monica;

    public string BundlePath { get; set; } = string.Empty;

    /// <summary>Selected install targets as "environment::target" keys, one checkbox each.</summary>
    public HashSet<string> SelectedTargets { get; } = new(StringComparer.OrdinalIgnoreCase);

    public string Port { get; set; } = string.Empty;

    public string? PlanDigest { get; set; }

    /// <summary>The bundle staged by the update page, kept for its configure preview/apply flow.</summary>
    public SetupBundleView? StagedUpdateBundle { get; set; }

    public IReadOnlyList<SetupAgentPresence>? PresenceCache { get; set; }

    /// <summary>True after host detection ran once, so revisits keep the user's target choices.</summary>
    public bool PresenceChecked { get; set; }

    public int? CachedPort { get; set; }

    /// <summary>
    /// The one full dashboard diagnosis of this wizard run. Host and runtime probing shells
    /// out to agent CLIs and takes seconds, so it starts in the background at startup,
    /// serves every later surface from this cache, and is replaced only by an explicit
    /// refresh or a product switch.
    /// </summary>
    public SetupDashboardView? DashboardCache { get; set; }

    public void ToggleLanguage()
    {
        Language = Language == Chinese ? English : Chinese;
        LanguageChanged?.Invoke();
    }

    /// <summary>
    /// Switches the wizard to one product definition, resetting every product-derived input
    /// (port default, staged update, approved plan digest, cached dashboard) so the next
    /// load observes the new product. A no-op for the same product.
    /// </summary>
    public void SwitchProduct(AgentProductDefinition product)
    {
        ArgumentNullException.ThrowIfNull(product);
        if (ReferenceEquals(product, CurrentProduct))
        {
            return;
        }

        CurrentProduct = product;
        Port = product.DefaultPort?.ToString() ?? string.Empty;
        PlanDigest = null;
        StagedUpdateBundle = null;
        DashboardCache = null;
        CachedPort = null;
    }

    /// <summary>Resolves one localized setup label for the current language.</summary>
    public string T(string key)
        => SetupText.Resolve(Language, key);
}

/// <summary>Static two-language label catalog for the setup UI.</summary>
public static class SetupText
{
    private const string Fallback = "en-US";

    private static readonly Dictionary<string, string> ChineseText = new()
    {
        ["title"] = "Monica Guide",
        ["dashboard"] = "总览",
        ["install"] = "安装 / 升级",
        ["uninstall"] = "卸载",
        ["refresh"] = "刷新",
        ["installed-version"] = "已安装版本",
        ["not-installed"] = "未安装",
        ["bundle-root"] = "安装目录",
        ["cockpit-status"] = "工作台状态",
        ["running"] = "运行中",
        ["stopped"] = "未运行",
        ["detected-agents"] = "检测到的宿主",
        ["detecting-hosts"] = "正在检测宿主…",
        ["status-checks"] = "配置状态",
        ["health-checks"] = "健康检查",
        ["go-install"] = "安装或升级",
        ["go-uninstall"] = "卸载",
        ["start-cockpit"] = "启动工作台",
        ["open-cockpit"] = "打开工作台",
        ["bundle-path"] = "发布包目录(bundle 根目录,含 app/)",
        ["validate"] = "校验",
        ["bundle-valid"] = "发布包有效",
        ["bundle-no-manifest"] = "目录有效,但没有 release manifest(开发构建)",
        ["select-targets"] = "选择要安装的目标",
        ["target-hint"] = "shared (~/.agents/skills) 适用于 Codex 等大多数 Agent 兼容宿主;claude (~/.claude/skills) 是 Claude Code 的专属发现目录。",
        ["port"] = "工作台端口",
        ["preview-plan"] = "预览计划",
        ["execute-install"] = "执行安装",
        ["preview-uninstall"] = "预览卸载",
        ["execute-uninstall"] = "执行卸载",
        ["confirm-uninstall"] = "我确认要移除以上列出的配置",
        ["plan-changes"] = "变更",
                ["plan-noop"] = "当前配置已完全一致,无需任何变更。",
        ["digest-note"] = "计划已锁定(digest 校验),执行时如有漂移会自动拒绝。",
        ["applied"] = "已执行",
        ["apply-failed"] = "执行未完成",
        ["bundle-missing-exe"] = "所选目录不是有效的发布包,请选择解压后的发布包根目录。",
        ["data-retention-note"] = "卸载只移除宿主配置与技能投影;产品数据(工作区、缓存、事务)保留在用户数据目录(Windows 为 %LOCALAPPDATA%\\<产品>,Linux 为 ~/.local/share/<产品>,macOS 为 ~/Library/Application Support/<产品>)。",
        ["ok"] = "正常",
        ["warning"] = "注意",
        ["error"] = "错误",
        ["action-replace-skill-directory"] = "替换技能目录",
        ["action-delete-skill-directory"] = "删除技能目录",
        ["action-write-file"] = "写入文件",
        ["action-delete-file"] = "删除文件",
        ["targets-required"] = "请至少选择一个安装目标。",
        ["port-invalid"] = "端口必须是 1-65535 之间的数字。",
        ["operating"] = "正在执行…",
        ["diagnosing"] = "正在诊断…",
        ["elapsed"] = "已用时",
        ["update"] = "更新",
        ["check-now"] = "检查更新",
        ["checking"] = "正在检查…",
        ["up-to-date"] = "已是最新版本。",
        ["update-available"] = "有可用更新",
        ["latest-version"] = "最新版本",
        ["release-notes"] = "发布说明",
        ["download-verify"] = "下载并校验",
        ["download-again"] = "重新下载",
        ["staged-ready"] = "新版本已下载并通过校验,可以预览升级计划。",
        ["update-flow-note"] = "升级会停止运行中的工作台,应用后自动重启;旧版本目录会保留,可随时回滚。",
        ["phase-download"] = "下载",
        ["phase-verify"] = "校验",
        ["phase-extract"] = "解压",
        ["phase-validate"] = "验证",
        ["rollback-title"] = "可回滚版本",
        ["rollback-hint"] = "以下历史版本仍保留在本机,切换即可回滚(同样走预览与确认)。",
        ["rollback"] = "回滚到此版本",
        ["update-restart-note"] = "正在重启工作台…",
        ["integration"] = "桌面集成",
        ["desktop-shortcut"] = "桌面快捷方式",
        ["startmenu-shortcut"] = "开始菜单快捷方式",
        ["autostart"] = "开机自动启动",
        ["integration-note"] = "快捷方式会打开工作台界面;自启在登录时后台启动,不弹出浏览器。",
        ["uninstall-integration-note"] = "卸载同时移除本产品自有的快捷方式与自启项(如有)。",
        ["stop-cockpit"] = "停止工作台",
        ["summary-ok"] = "一切就绪",
        ["summary-attention"] = "项需要关注",
        ["version"] = "版本",
        ["location"] = "位置",
        ["status"] = "状态",
        ["step-source"] = "发布包",
        ["step-options"] = "宿主与端口",
        ["step-execute"] = "预览并执行",
        ["install-to"] = "将安装",
        ["currently-installed"] = "当前已装",
        ["choose-folder-hint"] = "粘贴或输入解压后的发布包根目录(包含 app/ 文件夹)。",
        ["cancel"] = "取消",
        ["installed-targets"] = "已安装的技能目标",
        ["nothing-installed"] = "当前没有已记录的安装。",
        ["remove-target"] = "移除此目标",
        ["uninstall-everything"] = "全部卸载",
        ["workspaces"] = "工作区",
        ["workspaces-sub"] = "Monica 技能默认安装到各项目自己的目录;这里登记每个已初始化的工作区,并可安装、更新或忘记它们。",
        ["workspaces-empty"] = "还没有登记的工作区。在下方输入一个项目目录开始初始化。",
        ["ws-skills"] = "个技能",
        ["ws-instructions"] = "指令块",
        ["ws-install"] = "安装技能",
        ["ws-update"] = "更新技能",
        ["ws-forget"] = "忘记工作区",
        ["ws-add-title"] = "添加工作区",
        ["ws-path"] = "项目目录",
        ["ws-path-hint"] = "粘贴项目根目录;向导会检测项目类型(profile)、能力与框架版本。",
        ["ws-profile"] = "确认项目类型",
        ["ws-profile-desc-application"] = "在不动框架源码的前提下,基于 Monica 构建自己的应用;安装应用与 ProjectUnit 开发技能。",
        ["ws-profile-desc-extension-author"] = "开发独立发布、通过 NuGet 引用 Monica 的扩展包;安装框架、架构与扩展开发技能。",
        ["ws-profile-desc-framework-contributor"] = "在官方框架检出中开发和测试 Monica 源码;安装框架、架构、需求设计与测试技能。",
        ["ws-profile-desc-docs-contributor"] = "在 Monica.Docs 检出中编写中英双语文档;安装文档写作与应用技能。",
        ["ws-profile-hint"] = "检测到的候选:",
        ["ws-candidate"] = "候选",
        ["ws-architecture"] = "应用架构",
        ["ws-init-preview"] = "预览初始化",
        ["ws-apply-install"] = "向该工作区安装技能",
        ["ws-apply-forget"] = "移除该工作区的 Monica 内容",
        ["ws-already-initialized"] = "已初始化",
        ["ws-profile-required"] = "请选择项目类型。",
        ["ws-architecture-required"] = "application 类型需要选择一种架构。",
        ["ws-inspecting"] = "正在检测项目类型、能力与框架版本…",
        ["ws-detect-not-directory"] = "该路径不是存在的目录,请检查后重试。",
        ["ws-detect-framework"] = "检测到 Monica 框架仓库。",
        ["ws-detect-docs"] = "检测到 Monica.Docs 仓库。",
        ["ws-detect-extension"] = "检测到 Monica 扩展特征。",
        ["ws-detect-application"] = "检测到 Monica 包或项目引用。",
        ["ws-detect-ambiguous-both"] = "同时检测到应用与扩展特征;请显式选择项目类型。",
        ["ws-detect-ambiguous-unscanned"] = "检测到应用特征,但有界源码扫描无法排除扩展;请显式选择项目类型。",
        ["ws-detect-ambiguous-exhausted"] = "在有界源码扫描结束前没有发现决定性特征;请显式选择项目类型。",
        ["ws-detect-ambiguous-none"] = "未发现规范仓库标识或特征文件;请显式选择项目类型。",
        ["workspace-summary"] = "工作区",
        ["workspace-summary-healthy"] = "切健康",
        ["global-targets"] = "整机全局安装(可选)",
        ["global-targets-hint"] = "默认安装到项目目录;只有需要跨所有项目使用时才选择全局目录。",
        ["ws-group"] = "项目工作区",
        ["global-group"] = "全局目标",
        ["browse"] = "浏览…",
        ["browse-bundle-title"] = "选择发布包根目录(包含 app/ 的文件夹)",
        ["browse-workspace-title"] = "选择项目根目录",
    };

    private static readonly Dictionary<string, string> EnglishText = new()
    {
        ["title"] = "Monica Guide",
        ["dashboard"] = "Overview",
        ["install"] = "Install / Update",
        ["uninstall"] = "Uninstall",
        ["refresh"] = "Refresh",
        ["installed-version"] = "Installed version",
        ["not-installed"] = "Not installed",
        ["bundle-root"] = "Install location",
        ["cockpit-status"] = "Cockpit status",
        ["running"] = "Running",
        ["stopped"] = "Stopped",
        ["detected-agents"] = "Detected hosts",
        ["detecting-hosts"] = "Detecting hosts…",
        ["status-checks"] = "Configuration status",
        ["health-checks"] = "Health checks",
        ["go-install"] = "Install or update",
        ["go-uninstall"] = "Uninstall",
        ["start-cockpit"] = "Start cockpit",
        ["open-cockpit"] = "Open cockpit",
        ["bundle-path"] = "Release bundle directory (bundle root containing app/)",
        ["validate"] = "Validate",
        ["bundle-valid"] = "Bundle is valid",
        ["bundle-no-manifest"] = "Directory is valid but has no release manifest (development build)",
        ["select-targets"] = "Choose the install targets",
        ["target-hint"] = "shared (~/.agents/skills) serves Codex and most Agent Skills hosts; claude (~/.claude/skills) is Claude Code's own discovery directory.",
        ["port"] = "Cockpit port",
        ["preview-plan"] = "Preview plan",
        ["execute-install"] = "Apply install",
        ["preview-uninstall"] = "Preview uninstall",
        ["execute-uninstall"] = "Apply uninstall",
        ["confirm-uninstall"] = "I confirm removing the configuration listed above",
        ["plan-changes"] = "changes",
                ["plan-noop"] = "Everything already matches; no changes are needed.",
        ["digest-note"] = "The plan is digest-locked; drift at apply time is rejected automatically.",
        ["applied"] = "Applied",
        ["apply-failed"] = "Not completed",
        ["bundle-missing-exe"] = "The selected directory is not a valid release bundle; choose an extracted release bundle root.",
        ["data-retention-note"] = "Uninstall removes host configuration and skill projections only; product data (workspaces, caches, transactions) stays in the user data root (%LOCALAPPDATA%\\<product> on Windows, ~/.local/share/<product> on Linux, ~/Library/Application Support/<product> on macOS).",
        ["ok"] = "OK",
        ["warning"] = "Warning",
        ["error"] = "Error",
        ["action-replace-skill-directory"] = "Replace skill directories",
        ["action-delete-skill-directory"] = "Delete skill directories",
        ["action-write-file"] = "Write file",
        ["action-delete-file"] = "Delete file",
        ["targets-required"] = "Select at least one install target.",
        ["port-invalid"] = "The port must be a number between 1 and 65535.",
        ["operating"] = "Working…",
        ["diagnosing"] = "Running diagnostics…",
        ["elapsed"] = "Elapsed",
        ["update"] = "Update",
        ["check-now"] = "Check for updates",
        ["checking"] = "Checking…",
        ["up-to-date"] = "You are up to date.",
        ["update-available"] = "Update available",
        ["latest-version"] = "Latest version",
        ["release-notes"] = "Release notes",
        ["download-verify"] = "Download and verify",
        ["download-again"] = "Download again",
        ["staged-ready"] = "The new release is downloaded and verified; preview the upgrade plan next.",
        ["update-flow-note"] = "The upgrade stops a running cockpit and restarts it afterwards; the previous release directory is kept for rollback.",
        ["phase-download"] = "Download",
        ["phase-verify"] = "Verify",
        ["phase-extract"] = "Extract",
        ["phase-validate"] = "Validate",
        ["rollback-title"] = "Rollback candidates",
        ["rollback-hint"] = "These releases are still on disk; switching goes through the same preview and confirmation.",
        ["rollback"] = "Switch to this version",
        ["update-restart-note"] = "Restarting the cockpit…",
        ["integration"] = "Desktop integration",
        ["desktop-shortcut"] = "Desktop shortcut",
        ["startmenu-shortcut"] = "Start menu shortcut",
        ["autostart"] = "Start automatically at logon",
        ["integration-note"] = "Shortcuts open the cockpit UI; autostart runs it in the background at logon without opening a browser.",
        ["uninstall-integration-note"] = "Uninstall also removes product-owned shortcuts and the autostart entry when present.",
        ["stop-cockpit"] = "Stop cockpit",
        ["summary-ok"] = "All good",
        ["summary-attention"] = "items need attention",
        ["version"] = "Version",
        ["location"] = "Location",
        ["status"] = "Status",
        ["step-source"] = "Release bundle",
        ["step-options"] = "Hosts and port",
        ["step-execute"] = "Preview and apply",
        ["install-to"] = "Will install",
        ["currently-installed"] = "Currently installed",
        ["choose-folder-hint"] = "Paste the extracted release bundle root (the folder containing app/).",
        ["cancel"] = "Cancel",
        ["installed-targets"] = "Installed skill targets",
        ["nothing-installed"] = "No installation is recorded yet.",
        ["remove-target"] = "Remove this target",
        ["uninstall-everything"] = "Uninstall everything",
        ["workspaces"] = "Workspaces",
        ["workspaces-sub"] = "Monica skills install into each project's own directories by default; every initialized workspace is registered here for installing, updating, or forgetting.",
        ["workspaces-empty"] = "No workspaces are registered yet. Enter a project directory below to initialize one.",
        ["ws-skills"] = "skills",
        ["ws-instructions"] = "instructions",
        ["ws-install"] = "Install skills",
        ["ws-update"] = "Update skills",
        ["ws-forget"] = "Forget workspace",
        ["ws-add-title"] = "Add a workspace",
        ["ws-path"] = "Project directory",
        ["ws-path-hint"] = "Paste a project root; the wizard detects the profile, capabilities, and framework version.",
        ["ws-profile"] = "Confirm the profile",
        ["ws-profile-desc-application"] = "Build your own application on Monica without touching framework source; installs the application and ProjectUnit skills.",
        ["ws-profile-desc-extension-author"] = "Develop independently released extension packages that consume Monica through NuGet; installs the framework, architecture, and extension skills.",
        ["ws-profile-desc-framework-contributor"] = "Develop and test Monica framework source in the canonical checkout; installs the framework, architecture, requirement-design, and testing skills.",
        ["ws-profile-desc-docs-contributor"] = "Author bilingual documentation in a Monica.Docs checkout; installs the docs-authoring and application skills.",
        ["ws-profile-hint"] = "Detected candidate:",
        ["ws-candidate"] = "candidate",
        ["ws-architecture"] = "Application architecture",
        ["ws-init-preview"] = "Preview initialization",
        ["ws-apply-install"] = "Install skills into this workspace",
        ["ws-apply-forget"] = "Remove Monica from this workspace",
        ["ws-already-initialized"] = "already initialized",
        ["ws-profile-required"] = "Select a profile.",
        ["ws-architecture-required"] = "The application profile requires one architecture.",
        ["ws-inspecting"] = "Detecting the profile, capabilities, and framework version…",
        ["ws-detect-not-directory"] = "The path is not an existing directory; check it and try again.",
        ["ws-detect-framework"] = "Monica framework repository detected.",
        ["ws-detect-docs"] = "Monica.Docs repository detected.",
        ["ws-detect-extension"] = "Monica extension characteristics detected.",
        ["ws-detect-application"] = "Monica package or project references detected.",
        ["ws-detect-ambiguous-both"] = "Both application and extension characteristics were detected; select a profile explicitly.",
        ["ws-detect-ambiguous-unscanned"] = "Application characteristics were detected, but bounded source scanning could not rule out an extension; select a profile explicitly.",
        ["ws-detect-ambiguous-exhausted"] = "No decisive characteristics were found before the bounded source scan was exhausted; select a profile explicitly.",
        ["ws-detect-ambiguous-none"] = "No canonical repository identity or characteristic files were found; select a profile explicitly.",
        ["workspace-summary"] = "Workspaces",
        ["workspace-summary-healthy"] = "healthy",
        ["global-targets"] = "Machine-wide global install (optional)",
        ["global-targets-hint"] = "Project directories are the default; choose global catalogs only when the skills must serve every project.",
        ["ws-group"] = "Project workspaces",
        ["global-group"] = "Global targets",
        ["browse"] = "Browse…",
        ["browse-bundle-title"] = "Choose the release bundle root (the folder containing app/)",
        ["browse-workspace-title"] = "Choose the project root",
    };

    /// <summary>Resolves one label for the requested language, falling back to English and then the key.</summary>
    public static string Resolve(string language, string key)
    {
        var catalog = language == "zh-CN" ? ChineseText : EnglishText;
        return catalog.TryGetValue(key, out var value)
               || EnglishText.TryGetValue(key, out value)
            ? value
            : key;
    }
}
