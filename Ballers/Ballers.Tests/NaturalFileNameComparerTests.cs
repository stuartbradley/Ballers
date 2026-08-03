using Ballers.API.Services;

namespace Ballers.Tests
{
    public class NaturalFileNameComparerTests
    {
        private static List<string> Sort(params string[] names)
            => names.OrderBy(n => n, NaturalFileNameComparer.Instance).ToList();

        [Fact]
        public void SortsDigitRunsAsNumbers_NotText()
        {
            // A plain string sort puts photo10 before photo9, which would drop a
            // late goal into the middle of the first half.
            var sorted = Sort("photo10.jpg", "photo9.jpg", "photo1.jpg");

            Assert.Equal(["photo1.jpg", "photo9.jpg", "photo10.jpg"], sorted);
        }

        [Fact]
        public void KeepsRealMatchFilenamesInShootingOrder()
        {
            var sorted = Sort(
                "BallersleagueMW100051.jpg",
                "BallersleagueMW100011.jpg",
                "BallersleagueMW105481.jpg",
                "BallersleagueMW101201.jpg");

            Assert.Equal([
                "BallersleagueMW100011.jpg",
                "BallersleagueMW100051.jpg",
                "BallersleagueMW101201.jpg",
                "BallersleagueMW105481.jpg"
            ], sorted);
        }

        [Fact]
        public void LeadingZerosDoNotChangeOrder()
        {
            var sorted = Sort("img007.jpg", "img8.jpg", "img06.jpg");

            Assert.Equal(["img06.jpg", "img007.jpg", "img8.jpg"], sorted);
        }

        [Fact]
        public void MixedTextAndNumbersCompareSegmentBySegment()
        {
            var sorted = Sort("game2-shot10.jpg", "game2-shot2.jpg", "game10-shot1.jpg");

            Assert.Equal(["game2-shot2.jpg", "game2-shot10.jpg", "game10-shot1.jpg"], sorted);
        }

        [Fact]
        public void ComparisonIsCaseInsensitive()
        {
            Assert.Equal(0, NaturalFileNameComparer.Instance.Compare("Photo1.JPG", "photo1.jpg"));
        }

        [Fact]
        public void HandlesNullsAndEmpties()
        {
            Assert.True(NaturalFileNameComparer.Instance.Compare(null, "a.jpg") < 0);
            Assert.True(NaturalFileNameComparer.Instance.Compare("a.jpg", null) > 0);
            Assert.Equal(0, NaturalFileNameComparer.Instance.Compare(null, null));
            Assert.True(NaturalFileNameComparer.Instance.Compare("", "a.jpg") < 0);
        }

        [Fact]
        public void StrictPrefixSortsBeforeTheLongerName()
        {
            // Nothing differs until one name simply runs out.
            var sorted = Sort("IMG_1a", "IMG_1");

            Assert.Equal(["IMG_1", "IMG_1a"], sorted);
        }

        [Fact]
        public void PunctuationComparesByCharacter()
        {
            // Pinning this down rather than leaving it to chance: the names differ
            // at "-" against ".", and "-" is the lower character, so it comes first.
            var sorted = Sort("match1.jpg", "match1-extra.jpg");

            Assert.Equal(["match1-extra.jpg", "match1.jpg"], sorted);
        }
    }
}
