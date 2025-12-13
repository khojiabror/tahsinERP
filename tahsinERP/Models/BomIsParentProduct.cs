using System;
using System.Collections.Generic;
using System.Data.Entity;
using System.Linq;
using System.Web;

namespace tahsinERP.Models
{
    public class BomIsParentProduct
    {
        public static bool IsParentProduct(string childPno)
        {
            using (DBTHSNEntities db = new DBTHSNEntities())
            {
                if (string.IsNullOrWhiteSpace(childPno))
                {
                    return false;
                }

                // Find product by PNo
                var product = db.PRODUCTS
                    .Where(p => p.PNo == childPno && p.IsDeleted == false)
                    .FirstOrDefault();

                if (product == null)
                {
                    return false;
                }

                // Check if this product has a BOM (is a parent product)
                // A product is a parent if it has an active BOM_HEADER
                var bomHeader = db.BOM_HEADERS
                    .Where(bh => bh.ProductID == product.ID && 
                                 bh.IsActive == true)
                    .FirstOrDefault();

                return bomHeader != null;
            }
        }
    }
}