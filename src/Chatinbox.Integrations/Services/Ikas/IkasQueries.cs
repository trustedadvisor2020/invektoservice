namespace Chatinbox.Integrations.Services.Ikas;

/// <summary>
/// GraphQL query/mutation templates for ikas admin API.
/// Static const strings — no runtime allocation.
/// Field selections kept minimal to reduce response size; expand as needed.
/// Verified against live ikas API (dev-apitest store) 2026-03-03.
/// </summary>
internal static class IkasQueries
{
    // ── Orders ──

    public const string ListOrders = @"
        query($pagination: PaginationInput, $status: OrderStatusFilterInput, $customerEmail: StringFilterInput, $orderedAt: DateFilterInput, $search: String) {
            listOrder(pagination: $pagination, status: $status, customerEmail: $customerEmail, orderedAt: $orderedAt, search: $search) {
                data {
                    id
                    orderNumber
                    status
                    totalPrice
                    currencyCode
                    orderedAt
                    customer {
                        id
                        email
                        firstName
                        lastName
                        phone
                    }
                    shippingAddress {
                        city
                        district
                        addressLine1
                    }
                    orderLineItems {
                        id
                        productName
                        quantity
                        unitPrice
                    }
                    orderPackages {
                        id
                        orderPackageFulfillStatus
                        trackingInfo {
                            trackingNumber
                            trackingLink
                            cargoCompany
                        }
                    }
                }
                count
                hasNext
            }
        }";

    public const string GetOrder = @"
        query($id: StringFilterInput!) {
            listOrder(id: $id) {
                data {
                    id
                    orderNumber
                    status
                    totalPrice
                    currencyCode
                    orderedAt
                    customer {
                        id
                        email
                        firstName
                        lastName
                        phone
                    }
                    shippingAddress {
                        city
                        district
                        addressLine1
                        addressLine2
                        postalCode
                    }
                    orderLineItems {
                        id
                        productName
                        variantName
                        quantity
                        unitPrice
                        totalPrice
                    }
                    orderPackages {
                        id
                        orderPackageFulfillStatus
                        trackingInfo {
                            trackingNumber
                            trackingLink
                            cargoCompany
                        }
                    }
                }
            }
        }";

    // ── Products ──

    public const string ListProducts = @"
        query($pagination: PaginationInput, $name: StringFilterInput, $sku: StringFilterInput) {
            listProduct(pagination: $pagination, name: $name, sku: $sku) {
                data {
                    id
                    name
                    productVariantTypes {
                        id
                        variantValues {
                            id
                            name
                        }
                    }
                    variants {
                        id
                        sku
                        isActive
                        images {
                            imageUrl
                            isMain
                        }
                        prices {
                            sellPrice
                            currency
                        }
                        stocks {
                            stockCount
                        }
                    }
                }
                count
                hasNext
            }
        }";

    public const string GetProduct = @"
        query($id: StringFilterInput!) {
            listProduct(id: $id) {
                data {
                    id
                    name
                    description
                    productVariantTypes {
                        id
                        variantValues {
                            id
                            name
                        }
                    }
                    variants {
                        id
                        sku
                        isActive
                        images {
                            imageUrl
                            isMain
                        }
                        prices {
                            sellPrice
                            buyPrice
                            currency
                        }
                        stocks {
                            stockCount
                            stockLocationId
                        }
                    }
                }
            }
        }";

    // ── Customers ──

    public const string ListCustomers = @"
        query($pagination: PaginationInput, $phone: StringFilterInput, $email: StringFilterInput, $search: String) {
            listCustomer(pagination: $pagination, phone: $phone, email: $email, search: $search) {
                data {
                    id
                    firstName
                    lastName
                    email
                    phone
                    orderCount
                    totalOrderPrice
                    accountStatus
                    addresses {
                        city
                        district
                        addressLine1
                        phone
                    }
                }
                count
            }
        }";

    // ── Mutations ──

    public const string FulfillOrder = @"
        mutation($input: FulFillOrderInput!) {
            fulfillOrder(input: $input) {
                id
                orderNumber
                status
                orderPackages {
                    id
                    orderPackageFulfillStatus
                    trackingInfo {
                        trackingNumber
                        cargoCompany
                    }
                }
            }
        }";

    public const string UpdateOrderPackageStatus = @"
        mutation($input: UpdateOrderPackageStatusInput!) {
            updateOrderPackageStatus(input: $input) {
                id
                orderNumber
                status
                orderPackages {
                    id
                    orderPackageFulfillStatus
                }
            }
        }";
}
