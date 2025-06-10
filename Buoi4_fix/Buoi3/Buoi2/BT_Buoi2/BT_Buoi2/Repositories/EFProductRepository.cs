using BT_Buoi2.Models;
using Microsoft.EntityFrameworkCore;

namespace BT_Buoi2.Repositories
{
    public class EFProductRepository : IProductRepository
    {
        private readonly ApplicationDbContext _context;

        public EFProductRepository(ApplicationDbContext context)
        {
            _context = context;
        }

        public async Task AddAsync(Product product)
        {
            // Thêm đối tượng Product vào DbContext
            // EF Core sẽ tự động theo dõi các đối tượng ProductImage trong collection product.ImageUrls
            // và thêm chúng vào database khi SaveChangesAsync được gọi, nhờ vào mối quan hệ đã cấu hình.
            _context.Products.Add(product);
            await _context.SaveChangesAsync(); // <<-- DÒNG NÀY LƯU MỌI THAY ĐỔI VÀO DATABASE
        }

        public async Task DeleteAsync(int id)
        {
            // Lấy sản phẩm và các ảnh liên quan để có thể xóa file vật lý và đảm bảo cascade delete
            var product = await _context.Products
                                        .Include(p => p.ImageUrls) // Cần Include ImageUrls để xóa các file ảnh vật lý
                                        .FirstOrDefaultAsync(p => p.Id == id);
            if (product != null)
            {
                _context.Products.Remove(product);
                await _context.SaveChangesAsync(); // <<-- LƯU THAY ĐỔI
            }
        }

        public async Task<IEnumerable<Product>> GetAllAsync()
        {
            // Include Category để hiển thị tên danh mục trong trang danh sách
            // Không cần Include ImageUrls ở đây nếu trang danh sách không hiển thị ảnh phụ
            return await _context.Products
                                 .Include(p => p.Category)
                                 .ToListAsync();
        }

        public async Task<Product?> GetByIdAsync(int id)
        {
            // <<-- ĐÂY LÀ ĐIỂM RẤT QUAN TRỌNG CHO VIỆC CẬP NHẬT/HIỂN THỊ
            // Cần Include Category và ImageUrls để khi bạn lấy sản phẩm,
            // đối tượng Category và danh sách ảnh phụ của nó cũng được tải từ database.
            // Điều này là cần thiết cho các hành động Display, Update và Delete.
            return await _context.Products
                                 .Include(p => p.Category)
                                 .Include(p => p.ImageUrls) // <<-- DÒNG NÀY PHẢI CÓ ĐỂ TẢI ẢNH PHỤ HIỆN CÓ
                                 .FirstOrDefaultAsync(p => p.Id == id);
        }

        public async Task UpdateAsync(Product product)
        {
            // Khi bạn gọi Update trên một đối tượng Product đã được theo dõi bởi DbContext
            // (ví dụ: `existingProduct` trong controller), EF Core sẽ tự động:
            // 1. Cập nhật các thuộc tính của Product đã thay đổi.
            // 2. Thêm các ProductImage mới được thêm vào collection `product.ImageUrls`.
            // 3. Xóa các ProductImage bị gỡ khỏi collection (nếu có logic gỡ trong controller).
            _context.Products.Update(product); // Product ở đây là đối tượng đã được theo dõi (existingProduct)
            await _context.SaveChangesAsync(); // <<-- LƯU THAY ĐỔI
        }
    }
}
