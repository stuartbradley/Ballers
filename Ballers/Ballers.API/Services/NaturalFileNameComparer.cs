namespace Ballers.API.Services
{
    /// <summary>
    /// Orders filenames the way a person would: runs of digits compare as numbers,
    /// everything else compares as text. A plain string sort puts "photo10" before
    /// "photo9", which would shuffle a match out of sequence whenever a
    /// photographer's numbering rolls over a digit.
    /// </summary>
    public sealed class NaturalFileNameComparer : IComparer<string>
    {
        public static readonly NaturalFileNameComparer Instance = new();

        public int Compare(string? x, string? y)
        {
            if (ReferenceEquals(x, y)) return 0;
            if (x is null) return -1;
            if (y is null) return 1;

            int i = 0, j = 0;

            while (i < x.Length && j < y.Length)
            {
                if (char.IsDigit(x[i]) && char.IsDigit(y[j]))
                {
                    var startX = i;
                    var startY = j;

                    while (i < x.Length && char.IsDigit(x[i])) i++;
                    while (j < y.Length && char.IsDigit(y[j])) j++;

                    var numX = x.AsSpan(startX, i - startX).TrimStart('0');
                    var numY = y.AsSpan(startY, j - startY).TrimStart('0');

                    // Longer number wins once leading zeros are out of the way.
                    if (numX.Length != numY.Length)
                        return numX.Length - numY.Length;

                    var digits = numX.SequenceCompareTo(numY);
                    if (digits != 0) return digits;
                }
                else
                {
                    var c = char.ToUpperInvariant(x[i]).CompareTo(char.ToUpperInvariant(y[j]));
                    if (c != 0) return c;

                    i++;
                    j++;
                }
            }

            return (x.Length - i) - (y.Length - j);
        }
    }
}
