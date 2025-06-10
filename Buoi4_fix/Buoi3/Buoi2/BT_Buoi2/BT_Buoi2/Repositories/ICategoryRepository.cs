using BT_Buoi2.Models;

namespace BT_Buoi2.Repositories
{
    public interface ICategoryRepository
    {
      Task <IEnumerable<Category>> GetAllAsync();
       Task<Category> GetByIdAsync (int id);
        Task AddAsync(Category category);
        Task UpdateAsync(Category category);
        Task DeleteAsync(int id);
       
    }
}
