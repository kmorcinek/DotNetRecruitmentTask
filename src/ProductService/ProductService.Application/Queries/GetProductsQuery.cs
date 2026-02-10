using ProductService.Domain.Entities;

namespace ProductService.Application.Queries;

public record GetProductsQuery();

public record GetProductsQueryResult(List<Product> Products);
