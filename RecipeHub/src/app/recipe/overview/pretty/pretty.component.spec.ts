import { ComponentFixture, TestBed } from '@angular/core/testing';

import { PrettyComponent } from './pretty.component';

describe('PrettyComponent', () => {
  let component: PrettyComponent;
  let fixture: ComponentFixture<PrettyComponent>;

  beforeEach(async () => {
    await TestBed.configureTestingModule({
      declarations: [ PrettyComponent ]
    })
    .compileComponents();
  });

  beforeEach(() => {
    fixture = TestBed.createComponent(PrettyComponent);
    component = fixture.componentInstance;
    fixture.detectChanges();
  });

  it('should create', () => {
    expect(component).toBeTruthy();
  });
});
