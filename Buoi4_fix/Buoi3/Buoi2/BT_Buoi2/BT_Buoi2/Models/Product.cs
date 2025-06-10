using System.ComponentModel.DataAnnotations;

namespace BT_Buoi2.Models
{
    public class Product
    {
        public int Id { get; set; }
        [Required, StringLength(100)]
        public string Name { get; set; }
        [Range(0.01, 10000.00)]
        public decimal Price { get; set; }
        public string Description { get; set; }
        public int CategoryId { get; set; }

        // Các thuộc tính hiện có
        public Category? Category { get; set; }
        public string? ImageUrl { get; set; } // Đường dẫn đến hình ảnh đại diện
        public List<ProductImage>? ImageUrls { get; set; } // Danh sách các hình ảnh
    }
}
