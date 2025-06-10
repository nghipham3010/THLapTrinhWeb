using System.ComponentModel.DataAnnotations;

namespace BT_Buoi2.Models
{
    public class ProductImage
    {
        public int Id { get; set; }

        [Required(ErrorMessage = "URL hình ảnh không được để trống.")]
        public string Url { get; set; } = string.Empty; // Khởi tạo để tránh cảnh báo nullability

        // Khóa ngoại: liên kết ProductImage với Product
        // Đây là cột sẽ lưu Id của Product mà ảnh này thuộc về
        public int ProductId { get; set; }

        // Navigation property: Trỏ về đối tượng Product mà ảnh này thuộc về
        public Product? Product { get; set; } // Có thể là null nếu không được load hoặc Product không tồn tại
    }
}
