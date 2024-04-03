-- Source: https://wiki.postgresql.org/wiki/Disk_Usage

SELECT
  nspname || '.' || relname AS "relation",
  pg_size_pretty(pg_relation_size(c.oid)) AS "size"
FROM pg_class c
LEFT JOIN pg_namespace n ON (n.oid = c.relnamespace)
WHERE nspname NOT IN ('pg_catalog', 'information_schema')
ORDER BY pg_relation_size(c.oid) DESC;
