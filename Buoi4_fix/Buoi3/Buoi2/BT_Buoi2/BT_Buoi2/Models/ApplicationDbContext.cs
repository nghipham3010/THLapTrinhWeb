using Microsoft.EntityFrameworkCore;
using BT_Buoi2.Models;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
namespace BT_Buoi2.Models
{
    public class ApplicationDbContext : IdentityDbContext<ApplicationUser>
    {
        public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options) : base(options) { }

        // Đảm bảo có DbSet cho tất cả các Model bạn muốn lưu trữ
        public DbSet<Product> Products { get; set; }
        public DbSet<Category> Categories { get; set; }
        public DbSet<ProductImage> ProductImages { get; set; } // <<-- BẮT BUỘC PHẢI CÓ DÒNG NÀY

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            // Cấu hình mối quan hệ One-to-Many giữa Product và ProductImage
            // Một Product có nhiều ProductImage
            // Một ProductImage thuộc về một Product
            modelBuilder.Entity<Product>()
                .HasMany(p => p.ImageUrls)      // Product có collection ImageUrls
                .WithOne(pi => pi.Product)      // ProductImage có navigation property Product
                .HasForeignKey(pi => pi.ProductId) // Khóa ngoại trong ProductImage là ProductId
                .OnDelete(DeleteBehavior.Cascade); // Khi Product bị xóa, các ProductImage liên quan cũng bị xóa
                                                   // Thay đổi thành DeleteBehavior.Restrict/SetNull nếu muốn hành vi khác
        }
    }
}
