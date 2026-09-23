namespace Shop
{
    public interface IWriter
    {
        /// <summary>Appends the text to the file.</summary>
        void Write(string text);
    }
}
