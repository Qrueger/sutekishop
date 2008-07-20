﻿using System;
using System.Collections.Generic;
using System.Data.SqlClient;
using System.Linq;
using System.Web;
using System.Web.Mvc;
using Suteki.Common.Extensions;
using Suteki.Common.Repositories;
using Suteki.Common.Services;
using Suteki.Common.Validation;
using Suteki.Shop.ViewData;
using Suteki.Shop.Repositories;
using System.Security.Permissions;
using MvcContrib.Filters;
using Suteki.Shop.Services;
using System.Data.Linq;
using OrderableExtensions=Suteki.Common.Repositories.OrderableExtensions;

namespace Suteki.Shop.Controllers
{
    public class ProductController : ControllerBase
    {
        IRepository<Product> productRepository;
        IRepository<Category> categoryRepository;
        IRepository<ProductImage> productImageRepository;
        IHttpFileService httpFileService;
        ISizeService sizeService;
        IOrderableService<Product> productOrderableService;
        IOrderableService<ProductImage> productImageOrderableService;

        public ProductController(
            IRepository<Product> productRepository,
            IRepository<Category> categoryRepository,
            IRepository<ProductImage> productImageRepository,
            IHttpFileService httpFileService,
            ISizeService sizeService,
            IOrderableService<Product> productOrderableService,
            IOrderableService<ProductImage> productImageOrderableService)
        {
            this.productRepository = productRepository;
            this.categoryRepository = categoryRepository;
            this.productImageRepository = productImageRepository;
            this.httpFileService = httpFileService;
            this.sizeService = sizeService;
            this.productOrderableService = productOrderableService;
            this.productImageOrderableService = productImageOrderableService;
        }

        public override string GetControllerName()
        {
            return "";
        }

        public ActionResult Index(int id)
        {
            return RenderIndexView(id);
        }

        private ActionResult RenderIndexView(int id)
        {
            Category category = categoryRepository.GetById(id);

            AppendTitle(category.Name);

            var products = category.Products.InOrder();

            if (!CurrentUser.IsAdministrator)
            {
                products = products.Active();
            }

            return View("Index", ShopView.Data.WithProducts(products).WithCategory(category));
        }

        public ActionResult Item(string urlName)
        {
            return RenderItemView(urlName);
        }

        private ActionResult RenderItemView(string urlName)
        {
            Product product = productRepository.GetAll().WithUrlName(urlName);
            AppendTitle(product.Name);
            return View("Item", ShopView.Data.WithProduct(product));
        }

        [PrincipalPermission(SecurityAction.Demand, Role = "Administrator")]
        public ActionResult New(int id)
        {
            Product defaultProduct = new Product
            {
                ProductId = 0,
                CategoryId = id,
                Position = productOrderableService.NextPosition
            };

            return View("Edit", EditViewData.WithProduct(defaultProduct)); 
        }

        [PrincipalPermission(SecurityAction.Demand, Role = "Administrator")]
        public ActionResult Update(int productId)
        {
            Product product = null;
            if (productId == 0)
            {
                product = CreateDefaultProduct();
            }
            else
            {
                product = productRepository.GetById(productId);
            }

            try
            {
                ValidatingBinder.UpdateFrom(product, Request.Form);
                UpdateImages(product, Request);
                sizeService.WithValues(Request.Form).Update(product);

                if (productId == 0)
                {
                    productRepository.InsertOnSubmit(product);
                }

                try
                {
                    productRepository.SubmitChanges();
                }
                catch (SqlException sqlException)
                {
                    if (sqlException.Message.StartsWith("Violation of UNIQUE KEY constraint 'product_name_unique'"))
                    {
                        throw new ValidationException("Product names must be unique and there is already a product called '{0}'".With(product.Name));
                    }
                    throw;
                }
            }
            catch (ValidationException validationException)
            {
                return View("Edit", EditViewData.WithProduct(product).WithErrorMessage(validationException.Message));
            }


            return View("Edit", EditViewData.WithProduct(product).WithMessage("This product has been saved"));
        }

        private Product CreateDefaultProduct()
        {
            return new Product
            {
                Sizes = new EntitySet<Size>
                {
                    new Size { IsActive = false, Name = "-", IsInStock = true }
                }
            };
        }

        private void UpdateImages(Product product, HttpRequestBase request)
        {
            IEnumerable<Image> images = httpFileService.GetUploadedImages(request);
            foreach (Image image in images)
            {
                product.ProductImages.Add(new ProductImage 
                {
                    Image = image,
                    Position = productImageOrderableService.NextPosition
                });
            }
        }

        [PrincipalPermission(SecurityAction.Demand, Role = "Administrator")]
        public ActionResult Edit(int id)
        {
            return RenderEditView(id);
        }

        private ActionResult RenderEditView(int id)
        {
            Product product = productRepository.GetById(id);
            return View("Edit", EditViewData.WithProduct(product));
        }

        [PrincipalPermission(SecurityAction.Demand, Role = "Administrator")]
        public ActionResult MoveUp(int id, int position)
        {
            productOrderableService
                .MoveItemAtPosition(position)
                .ConstrainedBy(product => product.CategoryId == id)
                .UpOne();

            return RenderIndexView(id);
        }

        [PrincipalPermission(SecurityAction.Demand, Role = "Administrator")]
        public ActionResult MoveDown(int id, int position)
        {
            productOrderableService
                .MoveItemAtPosition(position)
                .ConstrainedBy(product => product.CategoryId == id)
                .DownOne();

            return RenderIndexView(id);
        }

        [PrincipalPermission(SecurityAction.Demand, Role = "Administrator")]
        public ActionResult MoveImageUp(int id, int position)
        {
            productImageOrderableService
                .MoveItemAtPosition(position)
                .ConstrainedBy(productImage => productImage.ProductId == id)
                .UpOne();

            return RenderEditView(id);
        }

        [PrincipalPermission(SecurityAction.Demand, Role = "Administrator")]
        public ActionResult MoveImageDown(int id, int position)
        {
            productImageOrderableService
                .MoveItemAtPosition(position)
                .ConstrainedBy(productImage => productImage.ProductId == id)
                .DownOne();

            return RenderEditView(id);
        }

        [PrincipalPermission(SecurityAction.Demand, Role = "Administrator")]
        public ActionResult DeleteImage(int id, int productImageId)
        {
            ProductImage productImage = productImageRepository.GetById(productImageId);
            productImageRepository.DeleteOnSubmit(productImage);
            productImageRepository.SubmitChanges();

            return RenderEditView(id);
        }

        [PrincipalPermission(SecurityAction.Demand, Role = "Administrator")]
        public ActionResult ClearSizes(int id)
        {
            Product product = productRepository.GetById(id);
            sizeService.Clear(product);
            productRepository.SubmitChanges();

            return View("Edit", EditViewData.WithProduct(product));
        }

        public ShopViewData EditViewData
        {
            get
            {
                return ShopView.Data.WithCategories(categoryRepository.GetAll().Alphabetical());
            }
        }
    }
}
