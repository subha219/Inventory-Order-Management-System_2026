-- Batch / lot / expiry tracking. The app also runs this automatically at startup (safe to run more than once).
IF OBJECT_ID(N'dbo.ProductBatch', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.ProductBatch (
        Id              nvarchar(50)   NOT NULL CONSTRAINT PK_ProductBatch PRIMARY KEY,
        IsDeleted       bit            NOT NULL CONSTRAINT DF_ProductBatch_IsDeleted DEFAULT (0),
        CreatedAtUtc    datetime2      NULL,
        CreatedById     nvarchar(450)  NULL,
        UpdatedAtUtc    datetime2      NULL,
        UpdatedById     nvarchar(450)  NULL,
        ProductId       nvarchar(50)   NULL,
        WarehouseId     nvarchar(50)   NULL,
        BatchNumber     nvarchar(255)  NULL,
        ManufactureDate datetime2      NULL,
        ExpiryDate      datetime2      NULL,
        Quantity        float          NULL,
        Notes           nvarchar(4000) NULL
    );
    CREATE INDEX IX_ProductBatch_ProductId  ON dbo.ProductBatch (ProductId);
    CREATE INDEX IX_ProductBatch_ExpiryDate ON dbo.ProductBatch (ExpiryDate);
END
