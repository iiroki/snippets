-- Source: https://stackoverflow.com/a/18910638

SELECT pg_size_pretty(pg_relation_size('<table_name>'));
