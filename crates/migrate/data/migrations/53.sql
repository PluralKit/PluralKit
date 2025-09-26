-- database version 53
-- add toggle for showing color codes on cv2 cards

alter table system_config add column card_show_color_hex bool default false;

update info set schema_version = 53;