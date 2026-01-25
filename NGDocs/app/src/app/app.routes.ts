import { Routes } from '@angular/router';
import { IndexComponent } from './pages/index/index.component';
import { GettingStartedComponent } from './pages/getting-started/getting-started.component';
import { TroubleshootingComponent } from './pages/troubleshooting/troubleshooting.component';
import { WorkItemsBoardComponent } from './pages/work-items-board/work-items-board.component';

export const routes: Routes = [
  { path: '', component: IndexComponent, pathMatch: 'full' },
  { path: 'getting-started', component: GettingStartedComponent },
  { path: 'troubleshooting', component: TroubleshootingComponent },
  { path: 'work-items-board', component: WorkItemsBoardComponent },
  { path: '**', redirectTo: '' }
];
