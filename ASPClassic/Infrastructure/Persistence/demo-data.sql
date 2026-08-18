-- Demo data — NOT harvested from the legacy source, and deliberately kept out of seed-data.sql,
-- which Modernizer regenerates on every run and would overwrite.
--
-- Defines one ordinary data view over the Navigation table so the add / edit / delete path can be
-- exercised end to end. The four system views (-1..-4) all edit the portal's own metadata, where a
-- mistake is awkward to undo; this one edits a small table with nothing depending on it.
--
-- Guarded the same way the harvested seed is: nothing is deleted, nothing is overwritten.

-- FieldFlags: 1 Show in Form, 2 Required, 4 Read Only, 8 Show in Items List, 16 Show in Search.
-- Flags:      1 Allow Edit, 2 Allow Add, 4 Allow Delete, 8 Allow Clone,
--             16 Enable Form, 32 Enable Items List.
-- FieldType:  1 Text, 3 Integer.

INSERT INTO [DataView] (
    [ViewID], [Title], [DataSource], [MainTable], [Primarykey],
    [ModificationProcedure], [ViewProcedure], [DeleteProcedure], [ViewDescription], [OrderBy],
    [Flags], [DataTableModifierButtonStyle], [DataTableFlags], [DataTableDefaultPageSize],
    [DataTablePagingStyle], [Published], [RowReorderColumn], [IsSystemObject], [CSSTable])
SELECT 1, 'Demo — Navigation Links', 'CrudeDefault', 'portal.Navigation', 'NavId',
       '', '', '', '<p>A demo view for trying add, edit and delete against a real table.</p>', 'NavOrder',
       63, 1, 61, 25,
       'full_numbers', 1, '', 0, 'table table-hover table-bordered table-striped'
WHERE NOT EXISTS (SELECT 1 FROM [DataView] WHERE [ViewID] = 1);

INSERT INTO [DataViewField] (
    [FieldID], [ViewID], [FieldLabel], [FieldSource], [FieldType], [FieldFlags], [FieldOrder],
    [DefaultValue], [MaxLength], [UriPath], [UriStyle], [LinkedTable], [LinkedTableValueField],
    [LinkedTableTitleField], [LinkedTableGroupField], [LinkedTableGlyphField],
    [LinkedTableTooltipField], [LinkedTableAddition], [Width], [Height],
    [FieldDescription], [FormatPattern], [FieldTooltip], [FieldIdentifier])
SELECT 1001, 1, 'Label', 'NavLabel', '1', 27, 1,
       '', 100, '', 1, '', '', '', '', '', '', '', 0, 0,
       'The text shown in the menu', '', 'Required', 'Field_1001'
WHERE NOT EXISTS (SELECT 1 FROM [DataViewField] WHERE [FieldID] = 1001);

INSERT INTO [DataViewField] (
    [FieldID], [ViewID], [FieldLabel], [FieldSource], [FieldType], [FieldFlags], [FieldOrder],
    [DefaultValue], [MaxLength], [UriPath], [UriStyle], [LinkedTable], [LinkedTableValueField],
    [LinkedTableTitleField], [LinkedTableGroupField], [LinkedTableGlyphField],
    [LinkedTableTooltipField], [LinkedTableAddition], [Width], [Height],
    [FieldDescription], [FormatPattern], [FieldTooltip], [FieldIdentifier])
SELECT 1002, 1, 'Order', 'NavOrder', '3', 27, 2,
       '10', 10, '', 1, '', '', '', '', '', '', '', 0, 0,
       'Position in the menu', '', '', 'Field_1002'
WHERE NOT EXISTS (SELECT 1 FROM [DataViewField] WHERE [FieldID] = 1002);

INSERT INTO [DataViewField] (
    [FieldID], [ViewID], [FieldLabel], [FieldSource], [FieldType], [FieldFlags], [FieldOrder],
    [DefaultValue], [MaxLength], [UriPath], [UriStyle], [LinkedTable], [LinkedTableValueField],
    [LinkedTableTitleField], [LinkedTableGroupField], [LinkedTableGlyphField],
    [LinkedTableTooltipField], [LinkedTableAddition], [Width], [Height],
    [FieldDescription], [FormatPattern], [FieldTooltip], [FieldIdentifier])
SELECT 1003, 1, 'Link', 'NavUri', '1', 25, 3,
       '', 300, '', 1, '', '', '', '', '', '', '', 0, 0,
       'Where the menu item goes', '', '', 'Field_1003'
WHERE NOT EXISTS (SELECT 1 FROM [DataViewField] WHERE [FieldID] = 1003);

INSERT INTO [DataViewField] (
    [FieldID], [ViewID], [FieldLabel], [FieldSource], [FieldType], [FieldFlags], [FieldOrder],
    [DefaultValue], [MaxLength], [UriPath], [UriStyle], [LinkedTable], [LinkedTableValueField],
    [LinkedTableTitleField], [LinkedTableGroupField], [LinkedTableGlyphField],
    [LinkedTableTooltipField], [LinkedTableAddition], [Width], [Height],
    [FieldDescription], [FormatPattern], [FieldTooltip], [FieldIdentifier])
SELECT 1004, 1, 'Icon', 'NavGlyph', '1', 9, 4,
       'fas fa-link', 100, '', 1, '', '', '', '', '', '', '', 0, 0,
       'Font Awesome class', '', '', 'Field_1004'
WHERE NOT EXISTS (SELECT 1 FROM [DataViewField] WHERE [FieldID] = 1004);

INSERT INTO [DataViewField] (
    [FieldID], [ViewID], [FieldLabel], [FieldSource], [FieldType], [FieldFlags], [FieldOrder],
    [DefaultValue], [MaxLength], [UriPath], [UriStyle], [LinkedTable], [LinkedTableValueField],
    [LinkedTableTitleField], [LinkedTableGroupField], [LinkedTableGlyphField],
    [LinkedTableTooltipField], [LinkedTableAddition], [Width], [Height],
    [FieldDescription], [FormatPattern], [FieldTooltip], [FieldIdentifier])
SELECT 1005, 1, 'Tooltip', 'NavTooltip', '1', 9, 5,
       '', 300, '', 1, '', '', '', '', '', '', '', 0, 0,
       'Hover text', '', '', 'Field_1005'
WHERE NOT EXISTS (SELECT 1 FROM [DataViewField] WHERE [FieldID] = 1005);

-- NOT NULL with no default in the generated schema, so the insert has to carry it or the row
-- cannot land. Given a field of its own rather than hidden, since a value someone must supply is
-- one they should be able to see.
INSERT INTO [DataViewField] (
    [FieldID], [ViewID], [FieldLabel], [FieldSource], [FieldType], [FieldFlags], [FieldOrder],
    [DefaultValue], [MaxLength], [UriPath], [UriStyle], [LinkedTable], [LinkedTableValueField],
    [LinkedTableTitleField], [LinkedTableGroupField], [LinkedTableGlyphField],
    [LinkedTableTooltipField], [LinkedTableAddition], [Width], [Height],
    [FieldDescription], [FormatPattern], [FieldTooltip], [FieldIdentifier])
SELECT 1006, 1, 'Open in Frame', 'OpenUriInIFRAME', '3', 1, 6,
       '0', 1, '', 1, '', '', '', '', '', '', '', 0, 0,
       '0 or 1', '', '', 'Field_1006'
WHERE NOT EXISTS (SELECT 1 FROM [DataViewField] WHERE [FieldID] = 1006);

INSERT INTO [DataViewField] (
    [FieldID], [ViewID], [FieldLabel], [FieldSource], [FieldType], [FieldFlags], [FieldOrder],
    [DefaultValue], [MaxLength], [UriPath], [UriStyle], [LinkedTable], [LinkedTableValueField],
    [LinkedTableTitleField], [LinkedTableGroupField], [LinkedTableGlyphField],
    [LinkedTableTooltipField], [LinkedTableAddition], [Width], [Height],
    [FieldDescription], [FormatPattern], [FieldTooltip], [FieldIdentifier])
SELECT 1007, 1, 'Data View', 'ViewID', '3', 9, 7,
       '0', 10, '', 1, '', '', '', '', '', '', '', 0, 0,
       'The view this link opens, 0 for none', '', '', 'Field_1007'
WHERE NOT EXISTS (SELECT 1 FROM [DataViewField] WHERE [FieldID] = 1007);

-- Put the demo view on the menu. Nothing creates a Navigation row when a view is added — that is
-- true of the legacy too — so it is added here explicitly.
INSERT INTO [Navigation] ([NavId], [NavLabel], [NavParentId], [NavOrder], [NavUri], [NavGlyph], [NavTooltip], [ViewID], [OpenUriInIFRAME])
SELECT 3, 'Demo — Navigation Links', NULL, 3, '', 'fas fa-flask', 'A demo view for trying add and edit', 1, 0
WHERE NOT EXISTS (SELECT 1 FROM [Navigation] WHERE [NavId] = 3);


-- ─────────────────────────────────────────────────────────────────────────────
-- A worked example: an Inventory screen, built the way any new screen is built.
--
-- Step 1 is the part the portal does NOT do for you. A data view is a screen over
-- a table that already exists; it neither creates the table nor stores the rows.
-- There is no EF entity for Inventory either — the portal reads and writes it
-- dynamically from the view definition, which is the whole point of the design.
CREATE TABLE IF NOT EXISTS [Inventory] (
    [ItemID]      INTEGER NOT NULL PRIMARY KEY AUTOINCREMENT,
    [SKU]         TEXT    NOT NULL,
    [ItemName]    TEXT    NOT NULL,
    [Quantity]    INTEGER NOT NULL DEFAULT 0,
    [UnitPrice]   REAL    NOT NULL DEFAULT 0,
    [InStock]     INTEGER NOT NULL DEFAULT 1,
    [Notes]       TEXT    NULL,
    -- Deliberately left without a DataViewField row below, so there is a column present with no
    -- field describing it — which is what "Auto-Initialize" on Manage Fields exists to fix.
    [Location]    TEXT    NOT NULL DEFAULT ''
);

INSERT INTO [Inventory] ([ItemID],[SKU],[ItemName],[Quantity],[UnitPrice],[InStock],[Notes])
SELECT 1,'WID-001','Widget, small',120,2.5,1,'Restock monthly'
WHERE NOT EXISTS (SELECT 1 FROM [Inventory] WHERE [ItemID] = 1);

INSERT INTO [Inventory] ([ItemID],[SKU],[ItemName],[Quantity],[UnitPrice],[InStock],[Notes])
SELECT 2,'WID-002','Widget, large',40,7.25,1,''
WHERE NOT EXISTS (SELECT 1 FROM [Inventory] WHERE [ItemID] = 2);

INSERT INTO [Inventory] ([ItemID],[SKU],[ItemName],[Quantity],[UnitPrice],[InStock],[Notes])
SELECT 3,'GDG-010','Gadget assembly',0,19.99,0,'Discontinued'
WHERE NOT EXISTS (SELECT 1 FROM [Inventory] WHERE [ItemID] = 3);

-- Step 2: the screen. MainTable and Primarykey are what tie it to the table above.
-- Flags 63 = Allow Edit + Add + Delete + Clone + Enable Form + Enable Items List.
INSERT INTO [DataView] (
    [ViewID], [Title], [DataSource], [MainTable], [Primarykey],
    [ModificationProcedure], [ViewProcedure], [DeleteProcedure], [ViewDescription], [OrderBy],
    [Flags], [DataTableModifierButtonStyle], [DataTableFlags], [DataTableDefaultPageSize],
    [DataTablePagingStyle], [Published], [RowReorderColumn], [IsSystemObject], [CSSTable])
SELECT 2, 'Inventory', 'CrudeDefault', 'Inventory', 'ItemID',
       '', '', '', '<p>Stock items. Built as a worked example of adding a screen.</p>', 'SKU',
       63, 1, 61, 25,
       'full_numbers', 1, '', 0, 'table table-hover table-bordered table-striped'
WHERE NOT EXISTS (SELECT 1 FROM [DataView] WHERE [ViewID] = 2);

-- Step 3 is normally the "Auto-Initialize" button on Manage Fields, which reads the
-- table's columns and writes these rows for you. They are spelled out here so the
-- screen works on a fresh database; on a real new table you would press the button.
-- FieldFlags: 1 Show in Form, 2 Required, 4 Read Only, 8 Show in Items List, 16 Show in Search.
-- FieldType:  1 Text, 2 Text Area, 3 Integer, 4 Decimal, 9 Boolean Switch.

INSERT INTO [DataViewField] (
    [FieldID], [ViewID], [FieldLabel], [FieldSource], [FieldType], [FieldFlags], [FieldOrder],
    [DefaultValue], [MaxLength], [UriPath], [UriStyle], [LinkedTable], [LinkedTableValueField],
    [LinkedTableTitleField], [LinkedTableGroupField], [LinkedTableGlyphField],
    [LinkedTableTooltipField], [LinkedTableAddition], [Width], [Height],
    [FieldDescription], [FormatPattern], [FieldTooltip], [FieldIdentifier])
SELECT 2001, 2, 'SKU', 'SKU', '1', 27, 1,
       '', 50, '', 1, '', '', '', '', '', '', '', 0, 0,
       'Stock keeping unit', '', '', 'Field_2001'
WHERE NOT EXISTS (SELECT 1 FROM [DataViewField] WHERE [FieldID] = 2001);

INSERT INTO [DataViewField] (
    [FieldID], [ViewID], [FieldLabel], [FieldSource], [FieldType], [FieldFlags], [FieldOrder],
    [DefaultValue], [MaxLength], [UriPath], [UriStyle], [LinkedTable], [LinkedTableValueField],
    [LinkedTableTitleField], [LinkedTableGroupField], [LinkedTableGlyphField],
    [LinkedTableTooltipField], [LinkedTableAddition], [Width], [Height],
    [FieldDescription], [FormatPattern], [FieldTooltip], [FieldIdentifier])
SELECT 2002, 2, 'Item Name', 'ItemName', '1', 27, 2,
       '', 200, '', 1, '', '', '', '', '', '', '', 0, 0,
       'What the item is', '', '', 'Field_2002'
WHERE NOT EXISTS (SELECT 1 FROM [DataViewField] WHERE [FieldID] = 2002);

INSERT INTO [DataViewField] (
    [FieldID], [ViewID], [FieldLabel], [FieldSource], [FieldType], [FieldFlags], [FieldOrder],
    [DefaultValue], [MaxLength], [UriPath], [UriStyle], [LinkedTable], [LinkedTableValueField],
    [LinkedTableTitleField], [LinkedTableGroupField], [LinkedTableGlyphField],
    [LinkedTableTooltipField], [LinkedTableAddition], [Width], [Height],
    [FieldDescription], [FormatPattern], [FieldTooltip], [FieldIdentifier])
SELECT 2003, 2, 'Quantity', 'Quantity', '3', 27, 3,
       '0', 10, '', 1, '', '', '', '', '', '', '', 0, 0,
       'Units on hand', '', '', 'Field_2003'
WHERE NOT EXISTS (SELECT 1 FROM [DataViewField] WHERE [FieldID] = 2003);

INSERT INTO [DataViewField] (
    [FieldID], [ViewID], [FieldLabel], [FieldSource], [FieldType], [FieldFlags], [FieldOrder],
    [DefaultValue], [MaxLength], [UriPath], [UriStyle], [LinkedTable], [LinkedTableValueField],
    [LinkedTableTitleField], [LinkedTableGroupField], [LinkedTableGlyphField],
    [LinkedTableTooltipField], [LinkedTableAddition], [Width], [Height],
    [FieldDescription], [FormatPattern], [FieldTooltip], [FieldIdentifier])
SELECT 2004, 2, 'Unit Price', 'UnitPrice', '4', 27, 4,
       '0', 10, '', 1, '', '', '', '', '', '', '', 0, 0,
       'Price per unit', '', '', 'Field_2004'
WHERE NOT EXISTS (SELECT 1 FROM [DataViewField] WHERE [FieldID] = 2004);

INSERT INTO [DataViewField] (
    [FieldID], [ViewID], [FieldLabel], [FieldSource], [FieldType], [FieldFlags], [FieldOrder],
    [DefaultValue], [MaxLength], [UriPath], [UriStyle], [LinkedTable], [LinkedTableValueField],
    [LinkedTableTitleField], [LinkedTableGroupField], [LinkedTableGlyphField],
    [LinkedTableTooltipField], [LinkedTableAddition], [Width], [Height],
    [FieldDescription], [FormatPattern], [FieldTooltip], [FieldIdentifier])
SELECT 2005, 2, 'In Stock', 'InStock', '9', 9, 5,
       '1', 1, '', 1, '', '', '', '', '', '', '', 0, 0,
       'Available to order', '', '', 'Field_2005'
WHERE NOT EXISTS (SELECT 1 FROM [DataViewField] WHERE [FieldID] = 2005);

INSERT INTO [DataViewField] (
    [FieldID], [ViewID], [FieldLabel], [FieldSource], [FieldType], [FieldFlags], [FieldOrder],
    [DefaultValue], [MaxLength], [UriPath], [UriStyle], [LinkedTable], [LinkedTableValueField],
    [LinkedTableTitleField], [LinkedTableGroupField], [LinkedTableGlyphField],
    [LinkedTableTooltipField], [LinkedTableAddition], [Width], [Height],
    [FieldDescription], [FormatPattern], [FieldTooltip], [FieldIdentifier])
SELECT 2006, 2, 'Notes', 'Notes', '2', 1, 6,
       '', 500, '', 1, '', '', '', '', '', '', '', 0, 0,
       'Free text', '', '', 'Field_2006'
WHERE NOT EXISTS (SELECT 1 FROM [DataViewField] WHERE [FieldID] = 2006);

-- ─────────────────────────────────────────────────────────────────────────────
-- Remove the "Auto-Init Fields" toolbar button.
--
-- A deliberate divergence from the legacy, asked for explicitly. The button is a row in
-- DataViewAction, so removing the row is how the portal itself removes a button — there is no
-- enabled flag to turn off. It has to be done HERE rather than by hand, because seed-data.sql
-- re-inserts the legacy rows on every start and would bring it straight back; this file runs after
-- that one, so the removal sticks across restarts.
--
-- The field list is unaffected: fields are still created by hand from Manage Fields, and the
-- generation itself is still reachable from any action a view chooses to define.
DELETE FROM [DataViewAction]
WHERE [ActionType] = 'url'
  AND [ActionExpression] LIKE '%mode=autoinit%';
