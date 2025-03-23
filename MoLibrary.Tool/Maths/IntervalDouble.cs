using System;
using System.Text.RegularExpressions;
using MoLibrary.Tool.Extensions;

namespace MoLibrary.Tool.Maths
{
    /// <summary>
    /// 数的类型
    /// </summary>
    public enum NumberType
    {
        /// <summary>
        /// 实数
        /// </summary>
        RealNumber,
        /// <summary>
        /// 无穷小
        /// </summary>
        Infinitesimal,
        /// <summary>
        /// 无穷大
        /// </summary>
        Infinity
    }
    /// <summary>
    /// 区间支持型Double对，即组成一个区间
    /// </summary>
    public class IntervalDoublePair
    {
        /// <summary>
        /// 左区间
        /// </summary>
        public IntervalDouble LeftInterval { get; set; }
        /// <summary>
        /// 右区间
        /// </summary>
        public IntervalDouble RightInterval { get; set; }
        /// <summary>
        /// 建立初始区间[0,0]
        /// </summary>
        public IntervalDoublePair()
        {
            LeftInterval = new IntervalDouble(0);
            RightInterval = new IntervalDouble(0);
        }
        /// <summary>
        /// 使用IntervalDouble对建立一个区间
        /// </summary>
        /// <param name="left"></param>
        /// <param name="right"></param>
        public IntervalDoublePair(IntervalDouble left, IntervalDouble right)
        {
            LeftInterval = left;
            RightInterval = right;
        }
        /// <summary>
        /// 使用double对建立一个区间
        /// </summary>
        /// <param name="left"></param>
        /// <param name="right"></param>
        /// <param name="leftIsOpen">左边是否是开区间</param>
        /// <param name="rightIsOpen">右边是否是开区间</param>
        public IntervalDoublePair(double left, double right, bool leftIsOpen = false, bool rightIsOpen = false)
        {
            if (left > right) throw new Exception("区间double不可以左边大于右边");
            LeftInterval = new IntervalDouble(left);
            RightInterval = new IntervalDouble(right);
        }
        /// <summary>
        /// 尝试从字符串中获取区间
        /// </summary>
        /// <param name="str"></param>
        /// <param name="intervalDoublePair"></param>
        /// <param name="force">若是能转为double但无法转为区间，强制转换为区间（即left=right值）</param>
        /// <returns></returns>
        public static bool TryGetIntervalDoublePair(string str, out IntervalDoublePair intervalDoublePair, bool force = false)
        {
            if (IntervalDouble.GetInterval(str, out var left, out IntervalDouble right, force))
            {
                intervalDoublePair = new IntervalDoublePair(left, right);
                return true;
            }

            intervalDoublePair = default;
            return false;
        }

        /// <summary>
        /// 获取区间长度（最大不超过int.MaxValue）
        /// </summary>
        /// <returns></returns>
        public int GetIntervalLength()
        {
            var left = LeftInterval.Value < int.MinValue ? int.MinValue : (int)LeftInterval.Value;
            var right = RightInterval.Value > int.MaxValue ? int.MaxValue : (int)RightInterval.Value;
            if (SubOk(right, left)) return right - left;
            return int.MaxValue;
        }

        private bool AddOk(int x, int y)
        {
            var z = x + y;
            switch (x)
            {
                case > 0 when y > 0 && z < 0:
                case < 0 when y < 0 && z > 0:
                    return false;
                default:
                    return true;
            }
        }

        private bool SubOk(int x, int y)
        {
            var z = x - y;
            switch (x)
            {
                case > 0 when y < 0 && z < 0:
                case < 0 when y > 0 && z > 0:
                    return false;
                default:
                    return true;
            }
        }

        /// <summary>
        /// 判断一个数是否在本区间中
        /// </summary>
        /// <param name="number"></param>
        /// <returns></returns>
        public bool IsInInterval(double? number) => number != null && IsInInterval(number.Value);
        /// <summary>
        /// 判断一个数是否在本区间中
        /// </summary>
        /// <param name="number"></param>
        /// <returns></returns>
        public bool IsInInterval(double number) => number <= RightInterval && number >= LeftInterval;

        /// <summary>
        /// 判断给定区间是否在本区间中（包含）
        /// </summary>
        /// <param name="interval"></param>
        /// <returns></returns>
        public bool IsInInterval(IntervalDoublePair interval) =>
            LeftInterval <= interval.LeftInterval && RightInterval >= interval.RightInterval;
        /// <summary>
        /// 获取在区间内左边最近整数，无穷小则返回int.MinValue
        /// </summary>
        /// <returns></returns>
        public int GetLeftIntervalNearestNumber()
        {
            var minValue = LeftInterval.NumType == NumberType.Infinitesimal ? int.MinValue : (int)Math.Ceiling(LeftInterval.Value);
            if (LeftInterval.IsOpen && minValue == (int)LeftInterval.Value) minValue += 1;
            return minValue;
        }
        /// <summary>
        /// 获取在区间内右边最近整数，无穷大则返回int.MaxValue
        /// </summary>
        /// <returns></returns>
        public int GetRightIntervalNearestNumber()
        {
            var maxValue = RightInterval.NumType == NumberType.Infinity
                ? int.MaxValue
                : (int)Math.Floor(RightInterval.Value);
            if (RightInterval.IsOpen && maxValue == (int)RightInterval.Value) maxValue -= 1;
            return maxValue;
        }
    }

    /// <summary>
    /// 区间支持型Double，默认闭区间，使用区间用小于等于或大于等于（当是开区间的时候实际上不会取到等于）
    /// </summary>
    public class IntervalDouble
    {

        /// <summary>
        /// 数的类型
        /// </summary>
        public NumberType NumType { get; set; }
        /// <summary>
        /// 值
        /// </summary>
        public double Value { get; set; }
        /// <summary>
        /// 是否是开区间
        /// </summary>
        public bool IsOpen { get; set; }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="value"></param>
        /// <param name="isOpen"></param>
        /// <param name="numberType"></param>
        public IntervalDouble(double value, bool isOpen = false, NumberType numberType = NumberType.RealNumber)
        {
            Value = value;
            IsOpen = isOpen;
            NumType = numberType;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="numberType"></param>
        public IntervalDouble(NumberType numberType)
        {
            NumType = numberType;
        }

        /// <summary>
        /// 指示是否是无穷数值（无穷大或无穷小）
        /// </summary>
        /// <param name="intervalDouble"></param>
        /// <returns></returns>
        public static bool IsInfinite(IntervalDouble intervalDouble)
        {
            return intervalDouble != null && intervalDouble.NumType != NumberType.RealNumber;
        }

        #region 公共方法
        /// <summary>
        /// 获取区间值（左到右）返回IntervalDouble类型，默认闭区间
        /// </summary>
        /// <param name="str"></param>
        /// <param name="intervalLeft">左区间 (、[</param>
        /// <param name="intervalRight">右区间 )、]</param>
        /// <param name="force">若是能转为double但无法转为区间，强制转换为区间（即left=right值）</param>
        /// <returns></returns>
        public static bool GetInterval(string str, out IntervalDouble intervalLeft, out IntervalDouble intervalRight, bool force = false)
        {
            intervalLeft = new IntervalDouble(0);
            intervalRight = new IntervalDouble(0);
            if (str.IsNullOrWhiteSpace()) return false;
            var regex = new Regex(@"[\[\(](-?\d*\.?\d+)?.+?(-?\d*\.?\d+)?[\]\)]");//匹配区间（浮点数以及负数支持）
            if (regex.IsMatch(str))
            {
                var groups = regex.Match(str).Groups;

                if (groups.Count == 3 && !groups[0].Value.IsNullOrWhiteSpace()) //第一组捕获组是全部
                {
                    if (double.TryParse(groups[1].Value, out var left))
                    {
                        if (groups[0].Value.StartsWith("(")) intervalLeft.IsOpen = true;
                        intervalLeft.Value = left;
                    }
                    else intervalLeft.NumType = NumberType.Infinitesimal;//无法判断或空则是认为是无穷小

                    if (double.TryParse(groups[2].Value, out var right))
                    {
                        if (groups[0].Value.EndsWith(")")) intervalRight.IsOpen = true;
                        intervalRight.Value = right;
                    }
                    else intervalRight.NumType = NumberType.Infinity;//无法判断或空则是认为是无穷大
                }

                if (intervalLeft > intervalRight) return false;
                return true;
            }
            else if (GetInterval(str, out var left, out double right))
            {
                intervalLeft.Value = left;
                intervalRight.Value = right;
                return true;
            }
            else if (force && double.TryParse(str, out var num))
            {
                intervalLeft.Value = intervalRight.Value = num;
                return true;
            }
            return false;
        }
        /// <summary>
        /// 获取区间值（左到右）
        /// </summary>
        /// <param name="str"></param>
        /// <param name="left">左值</param>
        /// <param name="right">右值</param>
        /// <param name="force">若是能转为double但无法转为区间，强制转换为区间（即left=right值）</param>
        /// <returns></returns>
        public static bool GetInterval(string str, out double left, out double right, bool force = false)
        {
            var result = str.Matches(@"(?<!\d)-?\d*\.?\d+");//支持匹配浮点数以及负数
            left = default;
            right = default;
            if (result == null || result.Count < 2)
            {
                if (force && Double.TryParse(str, out var num))
                {
                    left = right = num;
                    return true;
                }
                return false;
            }
            for (var i = 0; i < result.Count; i++)
            {
                if (i == 0)
                {
                    Double.TryParse(result[0], out left);
                }
                else if (i == 1)
                {
                    Double.TryParse(result[1], out right);
                }
                else break;
            }
            if (left > right) return false;
            return true;
        }
        /// <summary>
        /// 
        /// </summary>
        /// <returns></returns>
        public override string ToString()
        {
            return NumType switch
            {
                NumberType.Infinity => "+∞",
                NumberType.Infinitesimal => "-∞",
                _ => Value.ToString()
            };
        }
        #endregion

        #region 运算符重载 传入空值会报错
        /// <summary>
        /// 
        /// </summary>
        /// <param name="obj"></param>
        /// <returns></returns>
        public override bool Equals(object obj)
        {
            if (obj is IntervalDouble intervalDouble)
                return NumType == intervalDouble.NumType
                    && Value == intervalDouble.Value
                    && IsOpen == intervalDouble.IsOpen;
            return false;
        }
        /// <summary>
        /// 
        /// </summary>
        /// <returns></returns>
        public override int GetHashCode() //这里实验一下若是使用按照值计算hashcode是不是hashSet中就不能放多个一样的；若是按照地址计算又是不是可以放多个，虽然这违背了初衷
        {
            return base.GetHashCode();
        }
        public static bool operator >(IntervalDouble left, double right)
        {
            if (left.NumType == NumberType.Infinitesimal) return false;
            if (left.NumType == NumberType.Infinity) return true;
            return left.Value > right;
        }
        public static bool operator <(IntervalDouble left, double right)
        {
            if (left.NumType == NumberType.Infinity) return false;
            if (left.NumType == NumberType.Infinitesimal) return true;
            return left.Value < right;
        }
        public static bool operator <(double left, IntervalDouble right)
        {
            if (right.NumType == NumberType.Infinitesimal) return false;
            if (right.NumType == NumberType.Infinity) return true;
            return left < right.Value;
        }
        public static bool operator >(double left, IntervalDouble right)
        {
            if (right.NumType == NumberType.Infinity) return false;
            if (right.NumType == NumberType.Infinitesimal) return true;
            return left > right.Value;
        }

        public static bool operator >=(IntervalDouble left, double right)
        {
            if (left.NumType == NumberType.Infinitesimal) return false;
            if (left.NumType == NumberType.Infinity) return true;
            return left.IsOpen ? left.Value > right : left.Value >= right;
        }
        public static bool operator <=(IntervalDouble left, double right)
        {
            if (left.NumType == NumberType.Infinity) return false;
            if (left.NumType == NumberType.Infinitesimal) return true;
            return left.IsOpen ? left.Value < right : left.Value <= right;
        }
        public static bool operator <=(double left, IntervalDouble right)
        {
            if (right.NumType == NumberType.Infinitesimal) return false;
            if (right.NumType == NumberType.Infinity) return true;
            return right.IsOpen ? left < right.Value : left <= right.Value;
        }
        public static bool operator >=(double left, IntervalDouble right)
        {
            if (right.NumType == NumberType.Infinity) return false;
            if (right.NumType == NumberType.Infinitesimal) return true;
            return right.IsOpen ? left > right.Value : left >= right.Value;
        }


        public static bool operator >(IntervalDouble left, IntervalDouble right)
        {
            if (left.NumType == NumberType.Infinitesimal || right.NumType == NumberType.Infinity) return false;
            if ((left.NumType == NumberType.Infinity && right.NumType != NumberType.Infinity) || (right.NumType == NumberType.Infinitesimal && left.NumType != NumberType.Infinitesimal)) return true;
            return left.Value > right.Value;
        }

        public static bool operator <(IntervalDouble left, IntervalDouble right)
        {
            if (left.NumType == NumberType.Infinity || right.NumType == NumberType.Infinitesimal) return false;
            if ((right.NumType == NumberType.Infinity && left.NumType != NumberType.Infinity) || (left.NumType == NumberType.Infinitesimal && right.NumType != NumberType.Infinitesimal)) return true;
            return left.Value < right.Value;
        }
        public static bool operator ==(IntervalDouble left, IntervalDouble right)
        {
            return left.Equals(right);
        }
        public static bool operator !=(IntervalDouble left, IntervalDouble right)
        {
            return !left.Equals(right);
        }
        public static bool operator >=(IntervalDouble left, IntervalDouble right)
        {
            return left.IsOpen ? (left.Value > right.Value) : (left.Value > right.Value || left.Value == right.Value);
        }
        public static bool operator <=(IntervalDouble left, IntervalDouble right)
        {
            return left.IsOpen ? (left.Value < right.Value) : (left.Value < right.Value || left.Value == right.Value);
        }
        #endregion
    }
    /// <summary>
    /// 区间Double型
    /// </summary>
    public static class IntervalDoubleTool
    {
        /// <summary>
        /// 获取区间值（左到右）返回IntervalDouble类型，默认闭区间（[1,3)这种形式）
        /// </summary>
        /// <param name="str"></param>
        /// <param name="intervalLeft"></param>
        /// <param name="intervalRight"></param>
        /// <returns></returns>
        public static bool TryGetInterval(this string str, out IntervalDouble intervalLeft, out IntervalDouble intervalRight, bool force = false)
        {
            return IntervalDouble.GetInterval(str, out intervalLeft, out intervalRight, force);
        }

        /// <summary>
        /// 获取区间值（左到右）（1-3这种形式）
        /// </summary>
        /// <param name="str"></param>
        /// <param name="left">左值</param>
        /// <param name="right">右值</param>
        /// <returns></returns>
        public static bool TryGetInterval(this string str, out double left, out double right, bool force = false)
        {
            return IntervalDouble.GetInterval(str, out left, out right, force);
        }
    }
}
