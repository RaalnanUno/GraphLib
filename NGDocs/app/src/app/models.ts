export type DbMode = 'debug' | 'release';

export interface WorkItem {
  id: string;
  project: string;
  type: string;
  priority: number;
  title: string;
  summary: string;
  tags: string[];
  files: string[];
  asA?: string;
  iNeed?: string;
  soThat?: string;
  acceptance: string[];
  notes: string[];
}

export interface DbStatus {
  ok: boolean;
  mode: DbMode | null;
  selectedPath: string | null;
  selectedExists: boolean;
  knownPaths: {
    debug: string;
    release: string;
  };
}

export interface DbTablesResponse {
  ok: boolean;
  mode: DbMode;
  dbPath: string;
  tables: string[];
}

export interface DbColumn {
  name: string;
  type: string;
  notnull: boolean;
  defaultValue: any;
  pk: number;
}

export interface DbTableResponse {
  ok: boolean;
  mode: DbMode;
  dbPath: string;
  table: string;
  primaryKey: string | null;
  columns: DbColumn[];
  paging: { limit: number; offset: number; total: number };
  rows: Record<string, any>[];
}
