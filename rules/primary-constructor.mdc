在C# 12及以上版本中，类和结构体可以拥有**主构造函数**，它简化了用于初始化的参数声明。主构造函数的参数在整个类或结构体中都可用，可以用于状态初始化或直接在方法定义中使用。这一特性减少了重复的构造函数定义需求。

## 主构造函数的一般语法

- 主构造函数允许您声明在整个类或结构体范围内的参数。
- 如果类有基类，主构造函数参数可以传递给基类构造函数。
- 这些参数被视为**私有字段**，可以直接在类方法中使用。

### 基本示例：

```cs
public class C(bool b, int i, string s) // 带有三个参数的主构造函数
{
    public int I { get; set; } = i; // 状态初始化
    public string S // 直接使用构造函数参数的属性
    {
        get => s;
        set => s = value ?? throw new ArgumentNullException(nameof(S));
    }
    public C(string s) : this(true, 0, s) { } // 构造函数必须调用主构造函数
}
```