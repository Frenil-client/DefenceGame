using System;

namespace Synthesis.Core
{
    // STEP 1. 기반 도구 - long 기반 고정소수점 (스케일 1000).
    // 전투 수치를 float 로 누적하면 수만 틱 뒤 재현성이 깨진다 (ARCHITECTURE.md 4-3).
    // 내부 표현은 raw = 실수값 * 1000 의 정수다.
    public readonly struct Fixed : IEquatable<Fixed>, IComparable<Fixed>
    {
        public const long Scale = 1000;

        public readonly long raw;

        private Fixed(long rawValue)
        {
            raw = rawValue;
        }

        // ---- 생성자 헬퍼 ----

        public static Fixed FromRaw(long rawValue)
        {
            return new Fixed(rawValue);
        }

        public static Fixed FromInt(long value)
        {
            return new Fixed(value * Scale);
        }

        // 분자/분모 비율로 생성. 예: FromRatio(3, 2) = 1.5
        public static Fixed FromRatio(long numerator, long denominator)
        {
            if (denominator == 0) throw new DivideByZeroException("Fixed.FromRatio denominator 0");
            return new Fixed(numerator * Scale / denominator);
        }

        // 밀리 단위 정수를 그대로 raw 로 받는다. 예: FromMilli(1500) = 1.5
        public static Fixed FromMilli(long milli)
        {
            return new Fixed(milli);
        }

        public static readonly Fixed Zero = new Fixed(0);
        public static readonly Fixed One  = new Fixed(Scale);

        // ---- 변환 ----

        public long ToIntTruncated()
        {
            return raw / Scale;
        }

        public long ToIntRounded()
        {
            if (raw >= 0) return (raw + Scale / 2) / Scale;
            return (raw - Scale / 2) / Scale;
        }

        // 표시/디버그 전용. 로직 판단에 double 을 쓰지 않는다.
        public double ToDoubleForDisplay()
        {
            return (double)raw / Scale;
        }

        // ---- 연산자 ----

        public static Fixed operator +(Fixed a, Fixed b)
        {
            return new Fixed(a.raw + b.raw);
        }

        public static Fixed operator -(Fixed a, Fixed b)
        {
            return new Fixed(a.raw - b.raw);
        }

        public static Fixed operator -(Fixed a)
        {
            return new Fixed(-a.raw);
        }

        public static Fixed operator *(Fixed a, Fixed b)
        {
            return new Fixed(a.raw * b.raw / Scale);
        }

        public static Fixed operator /(Fixed a, Fixed b)
        {
            if (b.raw == 0) throw new DivideByZeroException("Fixed operator / by zero");
            return new Fixed(a.raw * Scale / b.raw);
        }

        public static bool operator ==(Fixed a, Fixed b) => a.raw == b.raw;
        public static bool operator !=(Fixed a, Fixed b) => a.raw != b.raw;
        public static bool operator <(Fixed a, Fixed b)  => a.raw <  b.raw;
        public static bool operator >(Fixed a, Fixed b)  => a.raw >  b.raw;
        public static bool operator <=(Fixed a, Fixed b) => a.raw <= b.raw;
        public static bool operator >=(Fixed a, Fixed b) => a.raw >= b.raw;

        // ---- 동등성 ----

        public bool Equals(Fixed other) => raw == other.raw;

        public override bool Equals(object obj) => obj is Fixed other && raw == other.raw;

        public override int GetHashCode() => raw.GetHashCode();

        public int CompareTo(Fixed other) => raw.CompareTo(other.raw);

        public override string ToString()
        {
            long whole = raw / Scale;
            long frac  = raw % Scale;
            if (frac < 0) frac = -frac;
            return whole + "." + frac.ToString("D3");
        }
    }
}
