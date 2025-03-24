using System.Collections.Generic;

namespace Koubot.Tool.KouData
{
    /// <summary>
    /// Kou静态数据库
    /// </summary>
    public static class KouStaticData
    {
        /// <summary>
        /// 中文标点与英文标点
        /// </summary>
        public static IReadOnlyDictionary<string, string> ZhToEnPunctuationDict { get; } =
            new Dictionary<string, string>
            {
                {"；", ";"}, {"。", "."}, {"：", ":"}, {"，", ","}, {"？", "?"}, {"！", "!"}, {"“", "\""}, {"”", "\""},
                {"（", "("}, {"）", ")"}, {"【", "["}, {"】", "]"}, {"《", "<"}, {"》", ">"}, {"…", "..."}, {"—", "-"},
                {"￥", "$"}, {"、", "\\"}, {"～", "~"}
            };
        /// <summary>
        /// bool值的转换
        /// </summary>
        public static IReadOnlyDictionary<string, bool> StringToBoolDict { get; } = new Dictionary<string, bool>
        {
            {"yes", true}, {"no", false}, {"1", true}, {"0", false}, {"是", true}, {"否", false}, {"对", true},
            {"错", false}, {"不对", false}, {"on", true}, {"off", false}, {"非", false},{"not", false},
            {"none", false},{"true", true},{"false", false},{"正确", true},{"错误", false},{"open", true},{"close", false},
            {"disable", false},{"enable", true},{"启动", true},{"关闭", false},{"启用", true},{"开", true},{"关", false},
            {"开启", true},{"打开", true},{"t", true},{"f", false}
        };
        /// <summary>
        /// 中文数学运算转对应符号
        /// </summary>
        public static IReadOnlyDictionary<string, string> ZhMathToSymbolMath { get; } = new Dictionary<string, string>()
        {
            {"加", "+"},{"减", "-"},{"乘", "*"},{"除","/"}
        };
        /// <summary>
        /// 翻页使用的下一页的关键词
        /// </summary>
        public static readonly HashSet<string> PageNextList = new()
        {
            "下",
            "n",
            "next",
            "下一页",
            "翻页",
            "f",
            "forward"
        };
        /// <summary>
        /// 翻页使用的上一页的关键词
        /// </summary>
        public static readonly HashSet<string> PagePreviousList = new()
        {
            "上",
            "p",
            "previous",
            "上一页",
            "back",
            "b"
        };

        /// <summary>
        /// 数据库增加的关键词
        /// </summary>
        public static readonly HashSet<string> AutoModelAddAction = new()
        {
            "add",
            "create",
            "增",
            "增加",
            "学",
            "学习",
            "添加",
            "加",
            "新增",
            "learn",
            "append",
            "a",
            "c",
            "post"
        };
        /// <summary>
        /// 数据库删除的关键词
        /// </summary>
        public static readonly HashSet<string> AutoModelDeleteAction = new()
        {
            "delete",
            "d",
            "remove",
            "删除",
            "丢弃",
            "删",
            "删掉",
            "忘记",
            "forget",
        };
        /// <summary>
        /// 数据库修改的关键词
        /// </summary>
        public static readonly HashSet<string> AutoModelModifyAction = new()
        {
            "modify",
            "update",
            "修改",
            "更改",
            "更新",
            "u",
            "alter",
            "改",
            "put",
            "patch"
        };
        /// <summary>
        /// 数据库查询的关键词
        /// </summary>
        public static readonly HashSet<string> AutoModelSearchAction = new()
        {
            "search",
            "retrieve",
            "检索",
            "搜索",
            "查询",
            "query",
            "r",
            "查",
            "get"
        };

        /// <summary>
        /// 动词列表
        /// </summary>
        public static readonly HashSet<string> Verb = new()
        {
            "看",
            "望",
            "瞥",
            "视",
            "盯",
            "瞧",
            "窥",
            "瞄",
            "眺",
            "瞪",
            "瞅",
            "听",
            "咬",
            "吞",
            "吐",
            "吮",
            "吸",
            "啃",
            "喝",
            "吃",
            "咀",
            "嚼",
            "搀",
            "抱",
            "搂",
            "扶",
            "捉",
            "擒",
            "掐",
            "推",
            "拿",
            "抽",
            "撕",
            "摘",
            "拣",
            "捡",
            "打",
            "播",
            "击",
            "捏",
            "撒",
            "按",
            "弹",
            "撞",
            "提",
            "扭",
            "捶",
            "持",
            "揍",
            "披",
            "捣",
            "搜",
            "托",
            "举",
            "拖",
            "擦",
            "敲",
            "挖",
            "抛",
            "掘",
            "抬",
            "插",
            "扔",
            "写",
            "抄",
            "抓",
            "捧",
            "掷",
            "撑",
            "摊",
            "倒",
            "摔",
            "劈",
            "画",
            "搔",
            "撬",
            "挥",
            "揽",
            "挡",
            "捺",
            "抚",
            "搡",
            "拉",
            "摸",
            "拍",
            "摇",
            "剪",
            "拎",
            "拔",
            "拧",
            "拨",
            "舞",
            "握",
            "攥",
            "退",
            "进",
            "奔",
            "跑",
            "赶",
            "趋",
            "遁",
            "逃",
            "立",
            "站",
            "跨",
            "踢",
            "跳",
            "走",
            "蹬",
            "窜",
            "屈",
            "转",
            "蜷",
            "卧",
            "仰",
            "躺",
            "睡",
            "趴",
            "蹲",
            "弯",
            "弓",
            "挺",
            "俯",
            "说",
            "笑",
            "飞",
            "唱",
            "坐",
            "闻",
            "想",
            "爱",
            "恨",
            "念",
            "喜欢",
            "希望",
            "担",
            "讨",
            "觉",
            "思"
        };
    }
}