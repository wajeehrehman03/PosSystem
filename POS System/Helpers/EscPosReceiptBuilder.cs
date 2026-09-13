using System.Text;

namespace PosWebApi.Helpers
{
    /// <summary>
    /// Builds a raw ESC/POS command byte stream for 58mm/80mm thermal receipt printers.
    /// Standard Epson ESC/POS command set (see the ESC/POS Command Reference) - no external
    /// package required, these are just the well-known command byte sequences.
    /// </summary>
    public class EscPosReceiptBuilder
    {
        private readonly MemoryStream _stream = new();
        private static readonly Encoding TextEncoding = Encoding.ASCII;

        private static readonly byte[] Init = { 0x1B, 0x40 };              // ESC @  - initialize printer
        private static readonly byte[] AlignLeft = { 0x1B, 0x61, 0x00 };   // ESC a 0 - left justify
        private static readonly byte[] AlignCenter = { 0x1B, 0x61, 0x01 }; // ESC a 1 - center justify
        private static readonly byte[] BoldOn = { 0x1B, 0x45, 0x01 };      // ESC E 1 - bold on
        private static readonly byte[] BoldOff = { 0x1B, 0x45, 0x00 };     // ESC E 0 - bold off
        private static readonly byte[] DoubleSize = { 0x1D, 0x21, 0x11 };  // GS ! 0x11 - double width+height
        private static readonly byte[] NormalSize = { 0x1D, 0x21, 0x00 };  // GS ! 0x00 - normal size
        private static readonly byte[] PartialCut = { 0x1D, 0x56, 0x01 };  // GS V 1 - partial cut
        private static readonly byte[] LineFeed = { 0x0A };

        public EscPosReceiptBuilder()
        {
            _stream.Write(Init, 0, Init.Length);
        }

        public EscPosReceiptBuilder Center()
        {
            _stream.Write(AlignCenter, 0, AlignCenter.Length);
            return this;
        }

        public EscPosReceiptBuilder Left()
        {
            _stream.Write(AlignLeft, 0, AlignLeft.Length);
            return this;
        }

        public EscPosReceiptBuilder Bold(bool on)
        {
            var bytes = on ? BoldOn : BoldOff;
            _stream.Write(bytes, 0, bytes.Length);
            return this;
        }

        public EscPosReceiptBuilder DoubleHeight(bool on)
        {
            var bytes = on ? DoubleSize : NormalSize;
            _stream.Write(bytes, 0, bytes.Length);
            return this;
        }

        public EscPosReceiptBuilder Line(string text = "")
        {
            var bytes = TextEncoding.GetBytes(text);
            _stream.Write(bytes, 0, bytes.Length);
            _stream.Write(LineFeed, 0, LineFeed.Length);
            return this;
        }

        public EscPosReceiptBuilder Divider(char c = '-', int width = 32)
        {
            return Line(new string(c, width));
        }

        /// <summary>Feeds a couple of blank lines (so the cut doesn't slice through text) and cuts.</summary>
        public EscPosReceiptBuilder FeedAndCut()
        {
            Line();
            Line();
            _stream.Write(PartialCut, 0, PartialCut.Length);
            return this;
        }

        public byte[] ToArray() => _stream.ToArray();
    }
}
