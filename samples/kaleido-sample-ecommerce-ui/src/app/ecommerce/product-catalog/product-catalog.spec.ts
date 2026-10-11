import { ComponentFixture, TestBed } from '@angular/core/testing';

import { ProductCatalog } from './product-catalog';

describe('ProductCatalog', () => {
  let component: ProductCatalog;
  let fixture: ComponentFixture<ProductCatalog>;

  beforeEach(async () => {
    // Shallow: the child query components read the Kaleido registry, which a unit test does not
    // load (no backend); this test covers the catalog component itself.
    await TestBed.configureTestingModule({
      imports: [ProductCatalog],
    })
      .overrideComponent(ProductCatalog, { set: { imports: [], template: '' } })
      .compileComponents();

    fixture = TestBed.createComponent(ProductCatalog);
    component = fixture.componentInstance;
    await fixture.whenStable();
  });

  it('should create', () => {
    expect(component).toBeTruthy();
  });
});
