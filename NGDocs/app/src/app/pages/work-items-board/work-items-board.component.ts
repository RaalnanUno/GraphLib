import { Component, OnDestroy, OnInit } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { Subject, takeUntil } from 'rxjs';

import { WorkItem } from '../../models';
import { WorkItemsService } from '../../services/work-items.service';

@Component({
  selector: 'app-work-items-board',
  standalone: true,
  imports: [CommonModule, FormsModule],
  templateUrl: './work-items-board.component.html'
})
export class WorkItemsBoardComponent implements OnInit, OnDestroy {
  private destroy$ = new Subject<void>();

  loading = false;
  error: string | null = null;

  all: WorkItem[] = [];
  filtered: WorkItem[] = [];

  q = '';
  selected: WorkItem | null = null;

  sortBy: 'priority' | 'id' | 'project' = 'priority';

  constructor(private workItems: WorkItemsService) {}

  ngOnInit(): void {
    this.reload();
  }

  ngOnDestroy(): void {
    this.destroy$.next();
    this.destroy$.complete();
  }

  reload(): void {
    this.loading = true;
    this.error = null;
    this.selected = null;

    this.workItems
      .list()
      .pipe(takeUntil(this.destroy$))
      .subscribe({
        next: (items) => {
          this.all = items ?? [];
          this.apply();
          this.loading = false;
        },
        error: (e) => {
          this.error = e?.message || 'Failed to load work items.';
          this.loading = false;
        }
      });
  }

  apply(): void {
    const q = this.q.trim().toLowerCase();

    let items = [...this.all];

    if (q) {
      items = items.filter((w) => {
        const hay = [
          w.id,
          w.project,
          w.type,
          w.title,
          w.summary,
          ...(w.tags || []),
          ...(w.files || [])
        ]
          .join(' ')
          .toLowerCase();
        return hay.includes(q);
      });
    }

    items.sort((a, b) => {
      if (this.sortBy === 'priority') return (a.priority ?? 999) - (b.priority ?? 999);
      if (this.sortBy === 'id') return (a.id ?? '').localeCompare(b.id ?? '');
      return (a.project ?? '').localeCompare(b.project ?? '');
    });

    this.filtered = items;

    // If selection fell out of filter, clear it.
    if (this.selected && !this.filtered.some((x) => x.id === this.selected!.id)) {
      this.selected = null;
    }
  }

  select(w: WorkItem): void {
    this.selected = w;
  }

  badgeClassForPriority(p: number): string {
    if (p <= 1) return 'text-bg-danger';
    if (p === 2) return 'text-bg-warning';
    if (p === 3) return 'text-bg-primary';
    return 'text-bg-secondary';
  }
}
