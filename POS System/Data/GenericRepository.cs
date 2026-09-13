using System.Collections.Generic;
using System.Linq;
using Microsoft.EntityFrameworkCore;

namespace PosWebApi.Data
{
    public interface IGenericRepository<T> where T : class
    {
        IEnumerable<T> GetAll();
        T? GetById(int id);
        void Add(T entity);
        void Update(T entity);
        bool Delete(int id);
        void SaveChanges();
    }

    public class GenericRepository<T> : IGenericRepository<T> where T : class
    {
        protected readonly AppDbContext _context;
        protected readonly DbSet<T> _dbSet;

        public GenericRepository(AppDbContext context)
        {
            _context = context ?? throw new ArgumentNullException(nameof(context));
            _dbSet = _context.Set<T>();
        }

        public virtual IEnumerable<T> GetAll()
        {
            try
            {
                return _dbSet.ToList();
            }
            catch (Exception ex)
            {
                throw new InvalidOperationException("Error retrieving entities from database", ex);
            }
        }

        public virtual T? GetById(int id)
        {
            if (id <= 0)
                return null;

            try
            {
                return _dbSet.Find(id);
            }
            catch (Exception ex)
            {
                throw new InvalidOperationException($"Error retrieving entity with ID {id}", ex);
            }
        }

        public virtual void Add(T entity)
        {
            if (entity == null)
                throw new ArgumentNullException(nameof(entity));

            try
            {
                _dbSet.Add(entity);
                _context.SaveChanges();
            }
            catch (DbUpdateException ex)
            {
                throw new InvalidOperationException("Error adding entity to database", ex);
            }
        }

        public virtual void Update(T entity)
        {
            if (entity == null)
                throw new ArgumentNullException(nameof(entity));

            try
            {
                _dbSet.Update(entity);
                _context.SaveChanges();
            }
            catch (DbUpdateException ex)
            {
                throw new InvalidOperationException("Error updating entity in database", ex);
            }
        }

        public virtual bool Delete(int id)
        {
            if (id <= 0)
                return false;

            try
            {
                var entity = GetById(id);
                if (entity == null)
                    return false;

                _dbSet.Remove(entity);
                _context.SaveChanges();
                return true;
            }
            catch (DbUpdateException ex)
            {
                throw new InvalidOperationException("Error deleting entity from database", ex);
            }
        }

        public virtual void SaveChanges()
        {
            try
            {
                _context.SaveChanges();
            }
            catch (DbUpdateException ex)
            {
                throw new InvalidOperationException("Error saving changes to database", ex);
            }
        }
    }
}
