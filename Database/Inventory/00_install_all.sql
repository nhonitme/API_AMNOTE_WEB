-- Install Inventory Opening SQL scripts
-- Company DB:
SOURCE 01_inventory_opening_menu_seed.sql;
SOURCE 02_sp_inventory_opening.sql;
SOURCE 05_inventory_bom_snapshot.sql;
SOURCE 07_sp_inventory_bom.sql;
-- Manager DB (PDF report catalog):
-- SOURCE 03_report_config_seed.sql;
