import { ComponentFixture, TestBed } from '@angular/core/testing';

import { WorkItemsBoard } from './work-items-board';

describe('WorkItemsBoard', () => {
  let component: WorkItemsBoard;
  let fixture: ComponentFixture<WorkItemsBoard>;

  beforeEach(async () => {
    await TestBed.configureTestingModule({
      imports: [WorkItemsBoard]
    })
    .compileComponents();

    fixture = TestBed.createComponent(WorkItemsBoard);
    component = fixture.componentInstance;
    await fixture.whenStable();
  });

  it('should create', () => {
    expect(component).toBeTruthy();
  });
});
