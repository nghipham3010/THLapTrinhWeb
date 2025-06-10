using BT_Buoi2.Models;
using BT_Buoi2.Repositories;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Authorization; // THÊM DÒNG NÀY
namespace BT_Buoi2.Controllers
{
    [Authorize]
    public class ProductController : Controller
    {
        private readonly IProductRepository _productRepository;
        private readonly ICategoryRepository _categoryRepository;
        private readonly ILogger<ProductController> _logger;
        private readonly IWebHostEnvironment _webHostEnvironment;

        public ProductController(IProductRepository productRepository,
                                 ICategoryRepository categoryRepository,
                                 ILogger<ProductController> logger,
                                 IWebHostEnvironment webHostEnvironment)
        {
            _productRepository = productRepository;
            _categoryRepository = categoryRepository;
            _logger = logger;
            _webHostEnvironment = webHostEnvironment;
        }

        // --- Action: Thêm sản phẩm mới (GET) ---
        public async Task<IActionResult> Add()
        {
            var categories = await _categoryRepository.GetAllAsync();
            ViewBag.Categories = new SelectList(categories, "Id", "Name");
            return View();
        }

        // --- Action: Thêm sản phẩm mới (POST) ---
        [HttpPost]
        public async Task<IActionResult> Add(Product product, IFormFile imageUrl, List<IFormFile> imageUrls)
        {
            // Bước 1: Loại bỏ lỗi ModelState tự động để tự xử lý việc binding ảnh
            // Khi dùng IFormFile, Model Binder sẽ không thể gán trực tiếp vào string/List<ProductImage>
            ModelState.Remove("ImageUrl");
            ModelState.Remove("ImageUrls"); // Rất quan trọng khi dùng List<ProductImage>?
            ModelState.Remove("Category");  // Loại bỏ nếu có lỗi binding tự động cho navigation property

            // Bước 2: Xử lý hình ảnh chính (ImageUrl)
            if (imageUrl == null || imageUrl.Length == 0)
            {
                ModelState.AddModelError("ImageUrl", "Hình ảnh chính là bắt buộc.");
            }
            else
            {
                if (!IsValidImage(imageUrl) || imageUrl.Length > 5 * 1024 * 1024) // Giới hạn 5MB
                {
                    ModelState.AddModelError("imageUrl", "Hình ảnh chính không hợp lệ (chỉ chấp nhận jpg, jpeg, png, gif và dưới 5MB).");
                }
                else
                {
                    product.ImageUrl = await SaveImage(imageUrl); // Lưu file và gán URL vào product
                }
            }

            // Bước 3: Xử lý các hình ảnh bổ sung (ImageUrls)
            if (imageUrls != null && imageUrls.Any(f => f.Length > 0))
            {
                // <<-- DÒNG NÀY LÀ BẮT BUỘC KHI `Product.ImageUrls` LÀ `List<ProductImage>?` (có thể null)
                // Nếu không có dòng này, `product.ImageUrls` sẽ là null
                // và `product.ImageUrls.Add()` sẽ gây ra `NullReferenceException`.
                product.ImageUrls = new List<ProductImage>();

                foreach (var file in imageUrls.Where(f => f.Length > 0))
                {
                    if (!IsValidImage(file) || file.Length > 5 * 1024 * 1024)
                    {
                        ModelState.AddModelError("imageUrls", $"Tệp '{file.FileName}' không hợp lệ (chỉ chấp nhận jpg, jpeg, png, gif và dưới 5MB).");
                    }
                    else
                    {
                        string imageUrlPath = await SaveImage(file);
                        // Tạo một đối tượng ProductImage mới và thêm vào collection của Product
                        product.ImageUrls.Add(new ProductImage { Url = imageUrlPath });
                        // EF Core sẽ tự động thiết lập ProductId khi lưu Product nhờ cấu hình quan hệ
                    }
                }
            }
            // Nếu không có ảnh phụ nào được chọn, product.ImageUrls sẽ vẫn là null (do khởi tạo ở đầu action)
            // hoặc List<ProductImage> rỗng (nếu đã khởi tạo ở trên và không có file nào hợp lệ)

            // Bước 4: Kiểm tra ModelState tổng thể và lưu sản phẩm vào database
            if (ModelState.IsValid)
            {
                try
                {
                    // Gọi repository để thêm sản phẩm.
                    // EF Core sẽ tự động phát hiện và lưu các ProductImage mới trong collection product.ImageUrls
                    // nhờ vào mối quan hệ đã được cấu hình trong ApplicationDbContext.
                    await _productRepository.AddAsync(product);
                    return RedirectToAction("Index"); // Chuyển hướng về trang danh sách sản phẩm sau khi thêm thành công
                }
                catch (DbUpdateException ex)
                {
                    // Xử lý lỗi liên quan đến database (ví dụ: ràng buộc khóa ngoại, lỗi dữ liệu)
                    _logger.LogError(ex, "Lỗi khi lưu sản phẩm vào cơ sở dữ liệu.");
                    ModelState.AddModelError("", $"Lỗi khi lưu sản phẩm vào cơ sở dữ liệu: {ex.InnerException?.Message ?? ex.Message}");
                }
                catch (Exception ex)
                {
                    // Xử lý các lỗi chung khác
                    _logger.LogError(ex, "Có lỗi xảy ra khi lưu sản phẩm.");
                    ModelState.AddModelError("", $"Có lỗi xảy ra khi lưu sản phẩm: {ex.Message}");
                }
            }

            // Bước 5: Nếu ModelState không hợp lệ hoặc có lỗi, tải lại dữ liệu cần thiết và trả về View
            // Điều này giúp người dùng nhìn thấy các lỗi xác thực và dữ liệu đã nhập vẫn còn trên form.
            var categories = await _categoryRepository.GetAllAsync();
            ViewBag.Categories = new SelectList(categories, "Id", "Name", product.CategoryId); // Giữ lại danh mục đã chọn
            return View(product); // Trả về model product để hiển thị lại dữ liệu đã nhập
        }

        // --- Action: Hiển thị danh sách sản phẩm (GET) ---
        public async Task<IActionResult> Index()
        {
            // Lấy tất cả sản phẩm. Đảm bảo ProductRepository.GetAllAsync() có Include() Category
            var products = await _productRepository.GetAllAsync();
            return View(products);
        }

        // --- Action: Hiển thị chi tiết sản phẩm (GET) ---
        public async Task<IActionResult> Display(int id)
        {
            // Lấy sản phẩm theo ID.
            // Rất quan trọng: _productRepository.GetByIdAsync(id) phải Include Category và ImageUrls
            var product = await _productRepository.GetByIdAsync(id);
            if (product == null)
            {
                return NotFound(); // Trả về 404 nếu không tìm thấy sản phẩm
            }
            return View(product);
        }

        // --- Action: Sửa sản phẩm (GET) ---
        // Hiển thị form với thông tin sản phẩm hiện có để chỉnh sửa
        public async Task<IActionResult> Update(int id)
        {
            // Lấy sản phẩm cần sửa. Đảm bảo Include Category và ImageUrls
            var product = await _productRepository.GetByIdAsync(id);
            if (product == null)
            {
                return NotFound();
            }
            var categories = await _categoryRepository.GetAllAsync();
            ViewBag.Categories = new SelectList(categories, "Id", "Name", product.CategoryId);
            return View(product);
        }

        // --- Action: Sửa sản phẩm (POST) ---
        // Xử lý cập nhật thông tin sản phẩm và ảnh
        [HttpPost]
        public async Task<IActionResult> Update(Product product, IFormFile imageUrl, List<IFormFile> imageUrls)
        {
            // Bước 1: Tải sản phẩm gốc từ cơ sở dữ liệu
            // Điều này cực kỳ quan trọng. Chúng ta không trực tiếp cập nhật 'product' được truyền từ form,
            // mà là 'existingProduct' được theo dõi bởi DbContext.
            // Đảm bảo GetByIdAsync đã Include ProductImage để các ảnh hiện có được tải.
            var existingProduct = await _productRepository.GetByIdAsync(product.Id);
            if (existingProduct == null)
            {
                return NotFound();
            }

            // Bước 2: Cập nhật các thuộc tính của existingProduct từ dữ liệu form
            existingProduct.Name = product.Name;
            existingProduct.Price = product.Price;
            existingProduct.Description = product.Description;
            existingProduct.CategoryId = product.CategoryId;

            // Bước 3: Loại bỏ lỗi ModelState tự động
            ModelState.Remove("ImageUrl");
            ModelState.Remove("ImageUrls");
            ModelState.Remove("Category");

            // Bước 4: Xử lý hình ảnh chính (ImageUrl)
            if (imageUrl != null && imageUrl.Length > 0)
            {
                if (!IsValidImage(imageUrl) || imageUrl.Length > 5 * 1024 * 1024)
                {
                    ModelState.AddModelError("imageUrl", "Chỉ cho phép các tệp hình ảnh (jpg, jpeg, png, gif) cho ảnh chính và kích thước dưới 5MB.");
                }
                else
                {
                    // Xóa ảnh cũ trước khi lưu ảnh mới
                    if (!string.IsNullOrEmpty(existingProduct.ImageUrl))
                    {
                        var oldImagePath = Path.Combine(_webHostEnvironment.WebRootPath, existingProduct.ImageUrl.TrimStart('/'));
                        if (System.IO.File.Exists(oldImagePath))
                        {
                            System.IO.File.Delete(oldImagePath);
                        }
                    }
                    existingProduct.ImageUrl = await SaveImage(imageUrl); // Gán URL ảnh mới
                }
            }
            // Nếu người dùng không chọn ảnh mới, ImageUrl cũ sẽ được giữ nguyên (từ existingProduct).

            // Bước 5: Xử lý các hình ảnh bổ sung (ImageUrls)
            // Logic này sẽ THÊM các ảnh mới được tải lên vào danh sách ảnh hiện có.
            // (Chưa có logic để xóa ảnh phụ hiện có qua UI)
            if (imageUrls != null && imageUrls.Any(f => f.Length > 0))
            {
                // <<-- DÒNG NÀY LÀ BẮT BUỘC KHI `Product.ImageUrls` LÀ `List<ProductImage>?` (có thể null)
                // Đảm bảo collection ImageUrls của existingProduct đã được khởi tạo.
                // Nếu GetByIdAsync đã Include nó, thì nó không null; nếu không, thì khởi tạo.
                existingProduct.ImageUrls = existingProduct.ImageUrls ?? new List<ProductImage>();

                foreach (var file in imageUrls.Where(f => f.Length > 0))
                {
                    if (!IsValidImage(file) || file.Length > 5 * 1024 * 1024)
                    {
                        ModelState.AddModelError("imageUrls", $"Tệp '{file.FileName}' không hợp lệ (chỉ chấp nhận jpg, jpeg, png, gif và dưới 5MB).");
                    }
                    else
                    {
                        string imageUrlPath = await SaveImage(file);
                        // Tạo và thêm đối tượng ProductImage mới vào existingProduct.ImageUrls
                        // EF Core sẽ nhận diện đây là một thực thể mới và thêm nó vào DB khi UpdateAsync được gọi.
                        existingProduct.ImageUrls.Add(new ProductImage { Url = imageUrlPath });
                    }
                }
            }

            // Bước 6: Kiểm tra ModelState và cập nhật sản phẩm
            if (ModelState.IsValid)
            {
                try
                {
                    // Gọi repository để cập nhật sản phẩm.
                    // EF Core sẽ tự động phát hiện các ProductImage mới được thêm vào existingProduct.ImageUrls
                    // và thêm chúng vào DB, đồng thời cập nhật các thuộc tính đã thay đổi của Product.
                    await _productRepository.UpdateAsync(existingProduct);
                    return RedirectToAction("Index");
                }
                catch (DbUpdateException ex)
                {
                    _logger.LogError(ex, "Lỗi khi cập nhật sản phẩm trong cơ sở dữ liệu.");
                    ModelState.AddModelError("", $"Lỗi khi cập nhật sản phẩm trong cơ sở dữ liệu: {ex.InnerException?.Message ?? ex.Message}");
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Có lỗi xảy ra khi cập nhật sản phẩm.");
                    ModelState.AddModelError("", $"Có lỗi xảy ra khi cập nhật sản phẩm: {ex.Message}");
                }
            }

            // Bước 7: Nếu ModelState không hợp lệ hoặc có lỗi, tải lại dữ liệu và trả về View
            var categories = await _categoryRepository.GetAllAsync();
            ViewBag.Categories = new SelectList(categories, "Id", "Name", product.CategoryId);
            return View(product); // Trả về 'product' từ form để giữ lại các giá trị người dùng đã nhập
        }

        // --- Action: Xóa sản phẩm (GET) ---
        // Hiển thị form xác nhận xóa
        public async Task<IActionResult> Delete(int id)
        {
            // Lấy sản phẩm cần xóa. Đảm bảo Include Category và ImageUrls để hiển thị đầy đủ thông tin
            var product = await _productRepository.GetByIdAsync(id);
            if (product == null)
            {
                return NotFound();
            }
            return View(product);
        }

        // --- Action: Xóa sản phẩm (POST) ---
        // Xử lý xóa sản phẩm khỏi cơ sở dữ liệu và các file ảnh vật lý
        [HttpPost, ActionName("Delete")]
        public async Task<IActionResult> DeleteConfirmed(int id)
        {
            try
            {
                // Lấy sản phẩm để xóa. Cần Include ImageUrls để xóa file vật lý
                var product = await _productRepository.GetByIdAsync(id);
                if (product == null)
                {
                    return NotFound();
                }

                // Xóa file hình ảnh chính vật lý
                if (!string.IsNullOrEmpty(product.ImageUrl))
                {
                    var imagePath = Path.Combine(_webHostEnvironment.WebRootPath, product.ImageUrl.TrimStart('/'));
                    if (System.IO.File.Exists(imagePath))
                    {
                        System.IO.File.Delete(imagePath);
                    }
                }
                // Xóa các file hình ảnh bổ sung vật lý
                if (product.ImageUrls != null)
                {
                    foreach (var image in product.ImageUrls)
                    {
                        var imagePath = Path.Combine(_webHostEnvironment.WebRootPath, image.Url.TrimStart('/'));
                        if (System.IO.File.Exists(imagePath))
                        {
                            System.IO.File.Delete(imagePath);
                        }
                    }
                }

                // Xóa sản phẩm khỏi DB. EF Core sẽ tự động xóa các ProductImage liên quan
                // nếu bạn đã cấu hình OnDelete(DeleteBehavior.Cascade) trong DbContext.
                await _productRepository.DeleteAsync(id);
                return RedirectToAction("Index");
            }
            catch (DbUpdateException ex)
            {
                _logger.LogError(ex, "Lỗi khi xóa sản phẩm khỏi cơ sở dữ liệu.");
                ModelState.AddModelError("", $"Lỗi khi xóa sản phẩm: {ex.InnerException?.Message ?? ex.Message}");
                // Nếu có lỗi, tải lại sản phẩm để hiển thị lại trang xác nhận xóa
                var product = await _productRepository.GetByIdAsync(id);
                return View("Delete", product);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Có lỗi xảy ra khi xóa sản phẩm.");
                ModelState.AddModelError("", $"Có lỗi xảy ra khi xóa sản phẩm: {ex.Message}");
                var product = await _productRepository.GetByIdAsync(id);
                return View("Delete", product);
            }
        }

        // --- Phương thức hỗ trợ: Kiểm tra định dạng hình ảnh ---
        private bool IsValidImage(IFormFile file)
        {
            var allowedExtensions = new[] { ".jpg", ".jpeg", ".png", ".gif" };
            var extension = Path.GetExtension(file.FileName).ToLowerInvariant();
            return allowedExtensions.Contains(extension);
        }

        // --- Phương thức hỗ trợ: Lưu hình ảnh vật lý và trả về URL ---
        private async Task<string> SaveImage(IFormFile image)
        {
            // Tạo tên tệp duy nhất bằng GUID để tránh xung đột tên file
            var fileName = Guid.NewGuid().ToString() + Path.GetExtension(image.FileName);
            // Kết hợp đường dẫn gốc của ứng dụng (wwwroot) với thư mục "images" và tên file
            var uploadPath = Path.Combine(_webHostEnvironment.WebRootPath, "images");
            var filePath = Path.Combine(uploadPath, fileName);

            // Đảm bảo thư mục "images" tồn tại, nếu không thì tạo mới
            if (!Directory.Exists(uploadPath))
            {
                Directory.CreateDirectory(uploadPath);
            }

            // Mở một luồng file để ghi dữ liệu hình ảnh vào
            using (var fileStream = new FileStream(filePath, FileMode.Create))
            {
                await image.CopyToAsync(fileStream); // Copy dữ liệu từ IFormFile vào file vật lý
            }
            // Trả về đường dẫn tương đối để lưu vào cơ sở dữ liệu và sử dụng trong các thẻ <img>
            return "/images/" + fileName;
        }
    }
}
