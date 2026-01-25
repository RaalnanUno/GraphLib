import { ComponentFixture, TestBed } from '@angular/core/testing';

import { Troubleshooting } from './troubleshooting';

describe('Troubleshooting', () => {
  let component: Troubleshooting;
  let fixture: ComponentFixture<Troubleshooting>;

  beforeEach(async () => {
    await TestBed.configureTestingModule({
      imports: [Troubleshooting]
    })
    .compileComponents();

    fixture = TestBed.createComponent(Troubleshooting);
    component = fixture.componentInstance;
    await fixture.whenStable();
  });

  it('should create', () => {
    expect(component).toBeTruthy();
  });
});
