using Synthesis.Core;

namespace Synthesis.Core.Tests
{
    // STEP 1. 검증 - 고정소수점 타입의 기본 연산과 문자열 파싱.
    public class FixedTests
    {
        [Fact]
        public void FromInt_And_Scale()
        {
            Assert.Equal(1000L, Fixed.One.raw);
            Assert.Equal(6000L, Fixed.FromInt(6).raw);
        }

        [Fact]
        public void Multiply_Keeps_Scale()
        {
            var a = Fixed.FromInt(2);
            var b = Fixed.FromInt(3);
            Assert.Equal(Fixed.FromInt(6), a * b);
        }

        [Fact]
        public void Divide_Is_Ratio()
        {
            var six = Fixed.FromInt(6);
            var four = Fixed.FromInt(4);
            Assert.Equal(Fixed.FromRatio(3, 2), six / four);
            Assert.Equal(1500L, (six / four).raw);
        }

        [Theory]
        [InlineData("1.5", 1500L)]
        [InlineData("0.001", 1L)]
        [InlineData("2", 2000L)]
        [InlineData("-2.25", -2250L)]
        [InlineData("0.40", 400L)]
        public void StringToFixed_Parses(string text, long expectedRaw)
        {
            Assert.Equal(expectedRaw, Synthesis.Core.Data.CsvUtil.StringToFixed(text).raw);
        }

        [Fact]
        public void ToString_Formats_Three_Decimals()
        {
            Assert.Equal("1.500", Fixed.FromRatio(3, 2).ToString());
        }
    }
}
