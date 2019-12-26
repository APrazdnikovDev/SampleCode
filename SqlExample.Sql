IF NOT EXISTS (SELECT * FROM sys.objects WHERE object_id = OBJECT_ID(N'[dbo].[nsfContractorsPOSM]') AND type in (N'U'))

CREATE TABLE [dbo].[nsfContractorsPOSM](
	[idGoods] [bigint] NOT NULL,
	[Value] [nvarchar](1500) NOT NULL,
 CONSTRAINT [PK_nsfContractorsPOSM] PRIMARY KEY CLUSTERED 
(
	[idGoods] ASC
)WITH (PAD_INDEX  = OFF, STATISTICS_NORECOMPUTE  = OFF, IGNORE_DUP_KEY = OFF, ALLOW_ROW_LOCKS  = ON, ALLOW_PAGE_LOCKS  = ON) ON [PRIMARY]
) ON [PRIMARY]

GO
IF  EXISTS (SELECT * FROM sys.objects WHERE object_id = OBJECT_ID(N'[Reports].[report_xls_ImportContractorsPOSM]') AND type in (N'P', N'PC'))
DROP PROCEDURE [Reports].[report_xls_ImportContractorsPOSM]
GO

CREATE PROCEDURE [Reports].[report_xls_ImportContractorsPOSM]               
				 @FileName        nvarchar(1000) 				
                ,@idPhysicalPerson         bigint                                                  --Код сотрудника создающий отчет
                ,@guid                     nvarchar(50)                    --! ключ уникальности, используется для временных таблиц
                ,@need_empty               bit = 0                                 --Признак вывода, 1 - шаблон, 0 - данные
                ,@Table_Test               nvarchar(50) = NULL      --Название таблицы, куда будет выгружены данные отчета, для тестирования
WITH EXECUTE AS OWNER
AS
BEGIN
-------------DEBUG---------------------------------------

--declare
-- @FileName        nvarchar(1000) = 'C:\Users\a.brytkov.SYSTTECH\Documents\SQL Server Management Studio\Projects\ImportPOSMCode\ImportPOSMCode_specimen.xlsx'
--,@idPhysicalPerson     bigint=0
--,@guid  nvarchar(50)='r1'
--,@need_empty   bit = 0
--,@Table_Test  nvarchar(50) = NULL


---------------------------------------------------
--SET DATEFORMAT dmy
SET TRANSACTION ISOLATION LEVEL READ UNCOMMITTED


SET NOCOUNT ON
DECLARE @temp_table nvarchar(50)

exec report_xls_getTempTable @guid, @temp_table output
if (@temp_table IS NOT NULL AND @temp_table <> '')
BEGIN
	--Выгрузить данные в таблицу для тестирования
	IF (@Table_Test<>'' and @Table_Test is not NULL)
	BEGIN   
		exec('SELECT * INTO dbo.[' + @Table_Test + '] FROM '+ @temp_table)
	END

	EXEC('SELECT * FROM [' + @temp_table+']')
	return
END

 

DECLARE @NameOutTable nvarchar(50)
SET @NameOutTable = '_exportXls' + @guid
              
if(@need_empty = 1)
BEGIN
SELECT
	'' [Причина/Ошибка] ,
	'' [Код POSM Чикаго],
	'' [Наименование POSM Чикаго] ,	
	'' [Наименование подрядчика] , 	
	'' [Код POSM подрядчика] , 	
	'' [Наименование POSM подрядчика] 

RETURN;
END;     



-----------Отчет----------------------------------------------------------------------------------------
DECLARE @idSession varchar(10) =CAST(@@SPID as varchar(10))
DECLARE @idAttribute bigint
----------Таблица отчета об Причина/Ошибках------------ 


IF OBJECT_ID('tempdb..#exportXls') IS NOT NULL DROP TABLE #exportXls

CREATE TABLE #exportXls(
	[Причина/Ошибка] [nvarchar](1000) NULL,
	[Код POSM Чикаго] [nvarchar](1000) NULL,
	[Наименование POSM Чикаго] [nvarchar](1000) NULL,
	[Наименование подрядчика]  [nvarchar](1000) NULL,
	[Код POSM подрядчика]  [nvarchar](1000) NULL,	
	[Наименование POSM подрядчика]  [nvarchar](1000) NULL	
)

--IF NOT EXISTS
--(
--	SELECT * 
--	FROM dbo.refAttributes a  
--	JOIN enums eo ON eo.id = a.idbaseobject AND eo.CodeKey ='Attrs_ObjectType_refGoods'
--	JOIN enums evt ON evt.id = a.idValueType AND evt.CodeKey ='Attrs_ValueType_String'
--	JOIN enums e ON e.id = a.idAttrType AND e.CodeKey ='Attrs_Type_Ref'
--	WHERE name = 'Код POSM'
--)
--BEGIN
--	INSERT #exportXls([Причина/Ошибка])VALUES(N'В справочнике Доп.атрибутов отсутствует доп. атрибут ''Код POSM''. Внесите соответствующее значение.')
--	GOTO RETURN_POINT
--END
--ELSE
--	SELECT TOP 1 @idAttribute = id  FROM dbo.refAttributes a WHERE name = 'Код POSM'
----------Таблица для содержимого файла------------ 
IF OBJECT_ID('tempdb..#POSM') IS NOT NULL DROP TABLE #POSM
CREATE TABLE #POSM(
	[idRow] [bigint] primary key NOT NULL,
	[Код POSM Чикаго] [nvarchar](1000) NULL,
	[Наименование POSM Чикаго] [nvarchar](1000) NULL,
	[Наименование подрядчика]  [nvarchar](1000) NULL,
	[Код POSM подрядчика]  [nvarchar](1000) NULL,	
	[Наименование POSM подрядчика]  [nvarchar](1000) NULL
)

---Получаем содержимое файла Excel------------
DECLARE @result int,@TableName varchar(30)= 'POSM'+@idSession
BEGIN TRY
exec @result = dbo.sp_ReadExcel 
   @isFirstRowColumnNames = 1
 , @FileName = @FileName
 , @SQLTableName = @TableName

 , @SheetName =''
 , @ColumnCount = 0
END TRY
BEGIN CATCH
	INSERT #exportXls([Причина/Ошибка]) VALUES('Ошибка чтения файла. ' + ERROR_MESSAGE() ) 
	
	GOTO RETURN_POINT
END CATCH

BEGIN TRY
exec('
DECLARE @ErrorMessage nvarchar(255)	
						
IF NOT EXISTS(select * from sys.columns where Name = N''Код POSM Чикаго''  and Object_ID = Object_ID(N''POSM'+@idSession+'''))
BEGIN 
	SET @ErrorMessage = ''Поле [Код POSM Чикаго] не существует или названо не правильно''
	INSERT #exportXls([Причина/Ошибка]) VALUES(@ErrorMessage ) 
	RAISERROR(@ErrorMessage,16, 1 );
END	

IF NOT EXISTS(select * from sys.columns where Name = N''Наименование подрядчика''  and Object_ID = Object_ID(N''POSM'+@idSession+'''))
BEGIN 
	SET @ErrorMessage = ''Поле [Наименование подрядчика] не существует или названо не правильно''
	INSERT #exportXls([Причина/Ошибка]) VALUES(@ErrorMessage ) 
	RAISERROR(@ErrorMessage,16, 1 );
END	

IF NOT EXISTS(select * from sys.columns where Name = N''Код POSM подрядчика''  and Object_ID = Object_ID(N''POSM'+@idSession+'''))
BEGIN 
	SET @ErrorMessage = ''Поле [Код POSM подрядчика] не существует или названо не правильно''
	INSERT #exportXls([Причина/Ошибка]) VALUES(@ErrorMessage ) 
	RAISERROR(@ErrorMessage,16, 1 );
END	

IF NOT EXISTS(select * from sys.columns where Name = N''Наименование POSM подрядчика''  and Object_ID = Object_ID(N''POSM'+@idSession+'''))
BEGIN 
	SET @ErrorMessage = ''Поле [Наименование POSM подрядчика] не существует или названо не правильно''
	INSERT #exportXls([Причина/Ошибка]) VALUES(@ErrorMessage ) 
	RAISERROR(@ErrorMessage,16, 1 );
END	


INSERT #POSM(
	idRow, 
	[Код POSM Чикаго], 
	[Наименование POSM Чикаго], 
	[Наименование подрядчика], 
	[Код POSM подрядчика], 
	[Наименование POSM подрядчика]
)
SELECT 
	idRow
	, LTRIM(RTRIM([Код POSM Чикаго]))
	, LTRIM(RTRIM([Наименование POSM Чикаго])) 
	, LTRIM(RTRIM([Наименование подрядчика])) 
	, LTRIM(RTRIM([Код POSM подрядчика])) 
	, LTRIM(RTRIM([Наименование POSM подрядчика])) 
FROM POSM'+@@SPID +'
WHERE 
	ISNULL([Код POSM Чикаго],'''') != '''' 
	OR ISNULL([Наименование подрядчика],'''') != '''' 
	OR ISNULL([Код POSM подрядчика],'''') != '''' 
	OR ISNULL([Наименование POSM подрядчика],'''') != '''' 

DROP TABLE  POSM'+@@SPID+'

IF EXISTS(SELECT * FROM #POSM WHERE ISNULL([Код POSM Чикаго],'''')='''' )
	INSERT #exportXls
(
	[Код POSM Чикаго], 
	[Наименование POSM Чикаго], 
	[Наименование подрядчика], 
	[Код POSM подрядчика], 
	[Наименование POSM подрядчика],
	[Причина/Ошибка]
) 
SELECT 
	[Код POSM Чикаго], 
	[Наименование POSM Чикаго], 
	[Наименование подрядчика], 
	[Код POSM подрядчика], 
	[Наименование POSM подрядчика],
	''Значение поля [Код POSM Чикаго] не может быть пустым''
FROM #POSM WHERE ISNULL([Код POSM Чикаго],'''') = '''' 

IF EXISTS(SELECT * FROM #POSM WHERE ISNULL([Наименование подрядчика],'''')='''' )
	INSERT #exportXls
(
	[Код POSM Чикаго], 
	[Наименование POSM Чикаго], 
	[Наименование подрядчика], 
	[Код POSM подрядчика], 
	[Наименование POSM подрядчика],
	[Причина/Ошибка]
) 
SELECT 
	[Код POSM Чикаго], 
	[Наименование POSM Чикаго], 
	[Наименование подрядчика], 
	[Код POSM подрядчика], 
	[Наименование POSM подрядчика],
	''Значение поля [Наименование подрядчика] не может быть пустым''
FROM #POSM WHERE ISNULL([Наименование подрядчика],'''') = '''' 

IF EXISTS(SELECT * FROM #POSM WHERE ISNULL([Код POSM подрядчика],'''')='''' )
	INSERT #exportXls
(
	[Код POSM Чикаго], 
	[Наименование POSM Чикаго], 
	[Наименование подрядчика], 
	[Код POSM подрядчика], 
	[Наименование POSM подрядчика],
	[Причина/Ошибка]
) 
SELECT 
	[Код POSM Чикаго], 
	[Наименование POSM Чикаго], 
	[Наименование подрядчика], 
	[Код POSM подрядчика], 
	[Наименование POSM подрядчика],
	''Значение поля [Код POSM подрядчика] не может быть пустым''
FROM #POSM WHERE ISNULL([Код POSM подрядчика],'''') = '''' 

IF EXISTS(SELECT * FROM #POSM WHERE ISNULL([Наименование POSM подрядчика],'''')='''' )
	INSERT #exportXls
(
	[Код POSM Чикаго], 
	[Наименование POSM Чикаго], 
	[Наименование подрядчика], 
	[Код POSM подрядчика], 
	[Наименование POSM подрядчика],
	[Причина/Ошибка]
) 
SELECT 
	[Код POSM Чикаго], 
	[Наименование POSM Чикаго], 
	[Наименование подрядчика], 
	[Код POSM подрядчика], 
	[Наименование POSM подрядчика],
	''Значение поля [Наименование POSM подрядчика] не может быть пустым''
FROM #POSM WHERE ISNULL([Наименование POSM подрядчика],'''') = '''' 
'
)


END TRY
BEGIN CATCH
	
	INSERT #exportXls([Причина/Ошибка]) VALUES('Ошибка чтения файла'+ERROR_MESSAGE() ) 
	GOTO RETURN_POINT
	
END CATCH
IF OBJECT_ID('tempdb..#POSM2') IS NOT NULL DROP TABLE #POSM2

IF   EXISTS(SELECT * FROM #POSM) 
BEGIN
	SELECT [Код POSM Чикаго]
		, [Наименование POSM Чикаго] 
		, [Code]
		, isSKUExists = CASE WHEN sku.id IS NULL THEN 0 ELSE 1 END
		, OperationOnAtrVal = CASE WHEN AtVal.idGoods IS NULL THEN 'I' ELSE 'U' END
		, sku.id as idGoods
	INTO #POSM2	
	FROM
	( 
	SELECT [Код POSM Чикаго]
		, [Наименование POSM Чикаго] 
		, [Code] = dbo.ConcatenateSemicolonDelimiter
		(
			[Наименование подрядчика] + '/' + 
			[Код POSM подрядчика]+ '/' + 
			[Наименование POSM подрядчика]
		)
		
	FROM #POSM posm
	GROUP BY 
	  [Код POSM Чикаго]
	, [Наименование POSM Чикаго] 
	)t
	OUTER APPLY
	(
		SELECT TOP 1 g.id,g.FullName,g.Article 
		FROM dbo.refGoods g
		JOIN dbo.enums e ON e.id = g.idGoodsType AND  (e.CodeKey = 'Goods_Type_Equipment' AND e.Value = 'POSM' OR e.CodeKey = 'Goods_Type_ExpensivePOSM' )
		WHERE g.Article = t.[Код POSM Чикаго]
		ORDER BY g.id
		
	)sku
	OUTER APPLY
	(
		SELECT TOP 1 av.idGoods
		FROM dbo.nsfContractorsPOSM av--dbo.refAttributesValues av
		WHERE av.idGoods = sku.id--av.idElement = sku.id AND av.idAttribute = @idAttribute
		ORDER BY  av.idGoods
	) AtVal
	
	BEGIN TRAN
	BEGIN TRY
	INSERT INTO dbo.nsfContractorsPOSM--dbo.refAttributesValues
	(
		idGoods, Value--idElement, idAttribute, Value, id, ObjectType
	)
	SELECT 
		
		 idGoods
		--, @idAttribute
		, [Code]
	    --, dbo.fn_getIdEx (281474976710657, 1)
	    --, 78
	FROM  #POSM2 POSM
	WHERE POSM.OperationOnAtrVal = 'I'
		AND  isSKUExists = 1  
	
	UPDATE dbo.nsfContractorsPOSM--dbo.refAttributesValues
	SET Value = [Code]
	FROM dbo.nsfContractorsPOSM av--dbo.refAttributesValues av
	JOIN #POSM2 POSM ON POSM.idGoods = av.idGoods--av.idElement 
	WHERE POSM.OperationOnAtrVal = 'U' AND POSM.isSKUExists = 1 
	
	IF @@TRANCOUNT > 0 AND XACT_STATE() = 1
	BEGIN
		COMMIT 
		
	END		
	ELSE
	BEGIN
		ROLLBACK
		INSERT #exportXls([Причина/Ошибка]) VALUES('Ошибка записи в БД. '+ERROR_MESSAGE() ) 
		GOTO RETURN_POINT
	END	
	END TRY
	BEGIN CATCH
		ROLLBACK
		INSERT #exportXls([Причина/Ошибка]) VALUES('Ошибка записи в БД. '+ERROR_MESSAGE() ) 
		
	END CATCH	
END



RETURN_POINT:
IF OBJECT_ID('tempdb..#POSM2') IS NOT NULL
INSERT #exportXls 
(   [Причина/Ошибка],
	[Код POSM Чикаго], 
	[Наименование POSM Чикаго] 
	
)
SELECT 
	'Такого кода товара типа POSM или дорогостоящий POSM не существует.',
	[Код POSM Чикаго], 
	[Наименование POSM Чикаго]
	
FROM #POSM2 POSM
WHERE isSKUExists = 0	

EXEC('IF OBJECT_ID(''POSM' + @idSession + ''') IS NOT NULL DROP TABLE  POSM' + @idSession)

-----------Отчет------------------------
                               --Заносим в список временных таблиц - rpTempTables
                EXEC('SELECT * INTO dbo.[' + @NameOutTable + '] FROM #exportXls')
                INSERT INTO rpTempTables (tableName, guid, createTime)
                               VALUES (@NameOutTable, @guid, getDate())              
                --Выгрузить данные в таблицу для тестирования

                IF (@Table_Test<>'' and @Table_Test is not NULL)
                BEGIN   
                               exec('SELECT * INTO dbo.[' + @Table_Test + '] FROM '+ @NameOutTable)
                END               
                IF object_id(N'tempdb..#FilterParams') is not null           drop table #FilterParams
                exec ('SELECT * FROM [' + @NameOutTable+'] ')



END


GO
