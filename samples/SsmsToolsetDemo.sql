/* ============================================================
   SSMS Toolset — demo database
   Creates tables (PK/FK), a migration table, a view, functions,
   and a stored procedure to showcase the toolset's features.
   ============================================================ */

IF DB_ID('SsmsToolsetDemo') IS NULL
    CREATE DATABASE SsmsToolsetDemo;
GO
USE SsmsToolsetDemo;
GO

/* ---- clean re-run ---------------------------------------- */
IF OBJECT_ID('dbo.usp_GetCustomerOrders')     IS NOT NULL DROP PROCEDURE dbo.usp_GetCustomerOrders;
IF OBJECT_ID('dbo.vCustomerOrderSummary')     IS NOT NULL DROP VIEW      dbo.vCustomerOrderSummary;
IF OBJECT_ID('dbo.ufn_CustomersInCity')       IS NOT NULL DROP FUNCTION  dbo.ufn_CustomersInCity;
IF OBJECT_ID('dbo.ufn_OrderTotal')            IS NOT NULL DROP FUNCTION  dbo.ufn_OrderTotal;
IF OBJECT_ID('dbo.OrderLine')                 IS NOT NULL DROP TABLE     dbo.OrderLine;
IF OBJECT_ID('dbo.OrderHeader')               IS NOT NULL DROP TABLE     dbo.OrderHeader;
IF OBJECT_ID('dbo.Product')                   IS NOT NULL DROP TABLE     dbo.Product;
IF OBJECT_ID('dbo.Category')                  IS NOT NULL DROP TABLE     dbo.Category;
IF OBJECT_ID('dbo.Customer')                  IS NOT NULL DROP TABLE     dbo.Customer;
IF OBJECT_ID('dbo.SchemaMigration')           IS NOT NULL DROP TABLE     dbo.SchemaMigration;
GO

/* ---- tables (PK + FK -> [pk]/[fk] markers) --------------- */
CREATE TABLE dbo.Customer (
    CustomerId  INT           IDENTITY(1,1) CONSTRAINT PK_Customer PRIMARY KEY,
    FullName    NVARCHAR(120) NOT NULL,
    Email       NVARCHAR(200) NULL,
    City        NVARCHAR(80)  NULL,
    CreatedAt   DATETIME2     NOT NULL CONSTRAINT DF_Customer_CreatedAt DEFAULT SYSUTCDATETIME()
);

CREATE TABLE dbo.Category (
    CategoryId  INT           IDENTITY(1,1) CONSTRAINT PK_Category PRIMARY KEY,
    Name        NVARCHAR(80)  NOT NULL
);

CREATE TABLE dbo.Product (
    ProductId   INT           IDENTITY(1,1) CONSTRAINT PK_Product PRIMARY KEY,
    Name        NVARCHAR(120) NOT NULL,
    UnitPrice   DECIMAL(10,2) NOT NULL,
    CategoryId  INT           NOT NULL CONSTRAINT FK_Product_Category REFERENCES dbo.Category(CategoryId)
);

CREATE TABLE dbo.OrderHeader (
    OrderId     INT           IDENTITY(1,1) CONSTRAINT PK_OrderHeader PRIMARY KEY,
    CustomerId  INT           NOT NULL CONSTRAINT FK_OrderHeader_Customer REFERENCES dbo.Customer(CustomerId),
    OrderDate   DATE          NOT NULL CONSTRAINT DF_OrderHeader_OrderDate DEFAULT CAST(SYSUTCDATETIME() AS DATE)
);

CREATE TABLE dbo.OrderLine (
    OrderLineId INT           IDENTITY(1,1) CONSTRAINT PK_OrderLine PRIMARY KEY,
    OrderId     INT           NOT NULL CONSTRAINT FK_OrderLine_Order   REFERENCES dbo.OrderHeader(OrderId),
    ProductId   INT           NOT NULL CONSTRAINT FK_OrderLine_Product REFERENCES dbo.Product(ProductId),
    Quantity    INT           NOT NULL,
    UnitPrice   DECIMAL(10,2) NOT NULL
);

/* migration-style table -> Samples > Show Migrations */
CREATE TABLE dbo.SchemaMigration (
    MigrationId INT           NOT NULL CONSTRAINT PK_SchemaMigration PRIMARY KEY,
    Name        NVARCHAR(200) NOT NULL,
    AppliedOn   DATETIME2     NOT NULL CONSTRAINT DF_SchemaMigration_AppliedOn DEFAULT SYSUTCDATETIME()
);
GO

/* ---- sample data ----------------------------------------- */
INSERT dbo.Customer (FullName, Email, City) VALUES
    (N'Ada Lovelace',  N'ada@example.com',   N'London'),
    (N'Alan Turing',   N'alan@example.com',  N'Manchester'),
    (N'Grace Hopper',  N'grace@example.com', N'New York');

INSERT dbo.Category (Name) VALUES (N'Hardware'), (N'Software'), (N'Books');

INSERT dbo.Product (Name, UnitPrice, CategoryId) VALUES
    (N'Keyboard', 45.00, 1), (N'Monitor', 210.00, 1),
    (N'IDE License', 99.00, 2), (N'SQL Guide', 32.50, 3);

INSERT dbo.OrderHeader (CustomerId, OrderDate) VALUES
    (1, '2026-08-01'), (2, '2026-08-15'), (1, '2026-09-01');

INSERT dbo.OrderLine (OrderId, ProductId, Quantity, UnitPrice) VALUES
    (1, 1, 2, 45.00), (1, 4, 1, 32.50),
    (2, 2, 1, 210.00), (3, 3, 3, 99.00);

INSERT dbo.SchemaMigration (MigrationId, Name) VALUES
    (1, N'0001_initial'), (2, N'0002_add_orders'), (3, N'0003_seed_data');
GO

/* ---- scalar function ------------------------------------- */
CREATE FUNCTION dbo.ufn_OrderTotal (@OrderId INT)
RETURNS DECIMAL(12,2)
AS
BEGIN
    DECLARE @Total DECIMAL(12,2);
    SELECT @Total = SUM(Quantity * UnitPrice)
    FROM dbo.OrderLine
    WHERE OrderId = @OrderId;
    RETURN ISNULL(@Total, 0);
END;
GO

/* ---- table-valued function ------------------------------- */
CREATE FUNCTION dbo.ufn_CustomersInCity (@City NVARCHAR(80))
RETURNS TABLE
AS
RETURN
    SELECT CustomerId, FullName, Email
    FROM dbo.Customer
    WHERE City = @City;
GO

/* ---- view (Total keyword -> search-in-definitions demo) -- */
CREATE VIEW dbo.vCustomerOrderSummary
AS
    SELECT c.CustomerId,
           c.FullName,
           o.OrderId,
           o.OrderDate,
           dbo.ufn_OrderTotal(o.OrderId) AS OrderTotal
    FROM dbo.Customer AS c
    JOIN dbo.OrderHeader AS o ON o.CustomerId = c.CustomerId;
GO

/* ---- stored procedure (params -> Execute / Columns) ------ */
CREATE PROCEDURE dbo.usp_GetCustomerOrders
    @CustomerId INT,
    @FromDate   DATE = NULL
AS
BEGIN
    SET NOCOUNT ON;
    SELECT o.OrderId,
           o.OrderDate,
           dbo.ufn_OrderTotal(o.OrderId) AS OrderTotal
    FROM dbo.OrderHeader AS o
    WHERE o.CustomerId = @CustomerId
      AND (@FromDate IS NULL OR o.OrderDate >= @FromDate)
    ORDER BY o.OrderDate DESC;
END;
GO

PRINT 'SsmsToolsetDemo ready — right-click the database in Object Explorer > SSMS Toolset.';
