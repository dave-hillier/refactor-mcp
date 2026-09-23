namespace Shop
{
    public class Product
    {
        public Product(string code)
        {
            Code = code;
        }

        protected Product()
        {
        }

        /// <summary>The stock code.</summary>
        public string Code { get; protected set; } = "";
    }

    public class Book : Product
    {
        public Book(string isbn)
        {
            Code = "ISBN-" + isbn;
        }
    }
}
