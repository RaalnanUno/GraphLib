import { Component, OnDestroy, OnInit } from '@angular/core';
import { CommonModule } from '@angular/common';
import { Subject, of, switchMap, takeUntil, catchError, timeout } from 'rxjs';

import { DbStatus, DbTableResponse, DbTablesResponse } from '../../models';
import { DbService } from '../../services/db.service';

@Component({
  selector: 'app-db-panel',
  standalone: true,
  imports: [CommonModule],
  templateUrl: './db-panel.component.html'
})
export class DbPanelComponent implements OnInit, OnDestroy {
  private destroy$ = new Subject<void>();
  private tableSelect$ = new Subject<string>();

  loadingStatus = false;
  loadingTables = false;
  loadingTable = false;

  status: DbStatus | null = null;
  tablesResp: DbTablesResponse | null = null;

  selectedTable: string | null = null;
  tableResp: DbTableResponse | null = null;

  error: string | null = null;

  constructor(private db: DbService) {}

  ngOnInit(): void {
    this.tableSelect$
      .pipe(
        switchMap((name) => {
          this.selectedTable = name;
          this.tableResp = null;
          this.error = null;
          this.loadingTable = true;

          return this.db.table(name, 50, 0).pipe(
            timeout({ first: 8000 }),
            catchError((e) => {
              this.error = this.pickError(e);
              return of(null);
            })
          );
        }),
        takeUntil(this.destroy$)
      )
      .subscribe((t) => {
        this.tableResp = t;
        this.loadingTable = false;
      });

    this.refreshAll();
  }

  ngOnDestroy(): void {
    this.destroy$.next();
    this.destroy$.complete();
  }

  refreshAll(): void {
    this.error = null;
    this.selectedTable = null;
    this.tableResp = null;

    this.loadingStatus = true;
    this.loadingTables = true;

    this.db
      .status()
      .pipe(
        timeout({ first: 5000 }),
        catchError((e) => {
          this.error = this.pickError(e);
          return of(null);
        }),
        switchMap((st) => {
          this.status = st;
          this.loadingStatus = false;

          if (!st?.selectedExists) {
            this.tablesResp = null;
            this.loadingTables = false;
            return of(null);
          }

          return this.db.tables().pipe(
            timeout({ first: 8000 }),
            catchError((e) => {
              this.error = this.pickError(e);
              return of(null);
            })
          );
        }),
        takeUntil(this.destroy$)
      )
      .subscribe((tables) => {
        this.tablesResp = tables;
        this.loadingTables = false;
      });
  }

  selectTable(name: string): void {
    this.tableSelect$.next(name);
  }

  private pickError(e: any): string {
    return e?.error?.error || e?.message || String(e) || 'Unknown error';
  }
}
