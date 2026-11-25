namespace ApplicationLayer.DTOs
{
    /// <summary>
    /// DTO de sortie exposé par l'API.
    /// </summary>
    public class ProductDTO
    {
        public int Id { get; set; }
        public string Name { get; set; } = null!;
        public string? Description { get; set; }
        public decimal Price { get; set; }
        public int Stock { get; set; }
    }
}
