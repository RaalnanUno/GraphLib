import { Component } from '@angular/core';
import { CommonModule } from '@angular/common';
import { DbPanelComponent } from '../../components/db-panel/db-panel.component';

@Component({
  selector: 'app-index',
  standalone: true,
  imports: [CommonModule, DbPanelComponent],
  templateUrl: './index.component.html'
})
export class IndexComponent {}
