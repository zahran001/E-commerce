﻿using AutoMapper;
using E_commerce.Services.ProductAPI.Models;
using E_commerce.Services.ProductAPI.Models.Dto;

namespace E_commerce.Services.ProductAPI
{
    public class MappingConfig
    {
        // add a static method
        public static MapperConfiguration RegisterMaps()
        {
            var mappingConfig = new MapperConfiguration(config =>
            {
                config.CreateMap<ProductDto, Product>();
                config.CreateMap<Product, ProductDto>();
                // reverse mapping

            });
            return mappingConfig;

        }
    }
}
// AutoMapper - as long as the property names are exactly the same, it will automatically map the properties.
// We have to register the mapping in the service collection in the startup class.

// AutoMapper only maps the properties that exist in the destination type.
// Any extra properties in the source model that do not have a corresponding match in the destination DTO will simply be ignored.