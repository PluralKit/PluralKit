-- SCHEMA VERSION 5: 2020-02-25
-- Creating functions to generate hids for when creating systems or members

CREATE OR REPLACE FUNCTION generatesystemhid()
RETURNS CHAR(5) as $$
DECLARE
    newHid CHAR(5);
BEGIN
    LOOP
        SELECT array_to_string(ARRAY(
            SELECT chr((ascii('b') + round(random() * 25)) :: integer) 
            FROM generate_series(1,5)), 
             '') INTO newHid;
        EXIT WHEN (SELECT COUNT(*) FROM systems WHERE hid = newHid) = 0;
    END LOOP;
    return newHid;
END;

$$ LANGUAGE plpgsql;


CREATE OR REPLACE FUNCTION generatememberhid()
RETURNS CHAR(5) as $$
DECLARE
    newHid CHAR(5);
BEGIN
    LOOP
        SELECT array_to_string(ARRAY(
            SELECT chr((ascii('b') + round(random() * 25)) :: integer) 
            FROM generate_series(1,5)), 
             '') INTO newHid;
        EXIT WHEN (SELECT COUNT(*) FROM members WHERE hid = newHid) = 0;
    END LOOP;
    return newHid;
END;
$$ LANGUAGE plpgsql;

update info set schema_version = 6;