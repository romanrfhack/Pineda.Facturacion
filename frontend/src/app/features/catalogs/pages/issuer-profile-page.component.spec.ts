import { TestBed } from '@angular/core/testing';
import { of, throwError } from 'rxjs';
import { IssuerProfilePageComponent } from './issuer-profile-page.component';
import { IssuerProfileApiService } from '../infrastructure/issuer-profile-api.service';
import { FeedbackService } from '../../../core/ui/feedback.service';
import { PermissionService } from '../../../core/auth/permission.service';

describe('IssuerProfilePageComponent', () => {
  const createApi = (overrides?: Partial<Record<keyof IssuerProfileApiService, unknown>>) => ({
    getActive: vi.fn().mockReturnValue(of({
      id: 4,
      legalName: 'Emisor Demo',
      rfc: 'AAA010101AAA',
      fiscalRegimeCode: '601',
      postalCode: '01000',
      cfdiVersion: '4.0',
      hasCertificateReference: true,
      hasPrivateKeyReference: true,
      hasPrivateKeyPasswordReference: true,
      hasLogo: true,
      logoFileName: 'logo.png',
      logoUpdatedAtUtc: '2026-03-24T12:00:00Z',
      pacEnvironment: 'Sandbox',
      isActive: true,
      createdAtUtc: '2026-03-24T12:00:00Z',
      updatedAtUtc: '2026-03-24T12:00:00Z'
    })),
    update: vi.fn().mockReturnValue(of({ outcome: 'Updated', isSuccess: true, id: 4 })),
    create: vi.fn().mockReturnValue(of({ outcome: 'Created', isSuccess: true, id: 4 })),
    getLogo: vi.fn().mockReturnValue(of(new Blob([new Uint8Array([0x89, 0x50, 0x4e, 0x47])], { type: 'image/png' }))),
    uploadLogo: vi.fn().mockReturnValue(of({ outcome: 'Updated', isSuccess: true, id: 4 })),
    removeLogo: vi.fn().mockReturnValue(of({ outcome: 'Removed', isSuccess: true, id: 4 })),
    ...overrides
  });

  beforeEach(() => {
    vi.spyOn(window, 'confirm').mockReturnValue(true);
    vi.stubGlobal('URL', {
      createObjectURL: vi.fn().mockReturnValue('blob:logo-preview'),
      revokeObjectURL: vi.fn()
    });
  });

  afterEach(() => {
    vi.restoreAllMocks();
    vi.unstubAllGlobals();
  });

  async function configure(apiOverrides?: Partial<Record<keyof IssuerProfileApiService, unknown>>) {
    const api = createApi(apiOverrides);

    await TestBed.configureTestingModule({
      imports: [IssuerProfilePageComponent],
      providers: [
        { provide: IssuerProfileApiService, useValue: api },
        { provide: FeedbackService, useValue: { show: vi.fn() } },
        {
          provide: PermissionService,
          useValue: { canWriteMasterData: vi.fn().mockReturnValue(true) }
        }
      ]
    }).compileComponents();

    const fixture = TestBed.createComponent(IssuerProfilePageComponent);
    fixture.detectChanges();
    await fixture.whenStable();
    fixture.detectChanges();
    return { fixture, api };
  }

  it('loads and previews the current issuer logo when it exists', async () => {
    const { api } = await configure();

    expect(api.getLogo).toHaveBeenCalledWith(4);
  });

  it('shows a validation error for invalid logo files', async () => {
    const { fixture } = await configure();
    const invalidFile = new File(['not-image'], 'logo.txt', { type: 'text/plain' });

    fixture.componentInstance['onLogoSelected']({ target: { files: [invalidFile], value: '' } } as unknown as Event);
    fixture.detectChanges();

    expect(fixture.componentInstance['logoError']()).toContain('Solo se permiten imágenes PNG, JPG, JPEG o WEBP');
  });

  it('updates the issuer profile and uploads a replacement logo', async () => {
    const { fixture, api } = await configure();
    const validFile = new File([new Uint8Array([0x89, 0x50, 0x4e, 0x47])], 'nuevo-logo.png', { type: 'image/png' });

    fixture.componentInstance['onLogoSelected']({ target: { files: [validFile], value: '' } } as unknown as Event);
    await fixture.componentInstance['save']();

    expect(api.update).toHaveBeenCalled();
    expect(api.uploadLogo).toHaveBeenCalledWith(4, validFile);
  });

  it('removes the current logo when requested and saved', async () => {
    const { fixture, api } = await configure();

    fixture.componentInstance['removeLogo']();
    await fixture.componentInstance['save']();

    expect(api.removeLogo).toHaveBeenCalledWith(4);
  });

  it('keeps compatibility when there is no logo yet', async () => {
    const { fixture, api } = await configure({
      getActive: vi.fn().mockReturnValue(of({
        id: 4,
        legalName: 'Emisor Demo',
        rfc: 'AAA010101AAA',
        fiscalRegimeCode: '601',
        postalCode: '01000',
        cfdiVersion: '4.0',
        hasCertificateReference: true,
        hasPrivateKeyReference: true,
        hasPrivateKeyPasswordReference: true,
        hasLogo: false,
        logoFileName: null,
        logoUpdatedAtUtc: null,
        pacEnvironment: 'Sandbox',
        isActive: true,
        createdAtUtc: '2026-03-24T12:00:00Z',
        updatedAtUtc: '2026-03-24T12:00:00Z'
      })),
      getLogo: vi.fn().mockReturnValue(throwError(() => ({ status: 404 })))
    });

    expect(api.getLogo).not.toHaveBeenCalled();
    expect(fixture.nativeElement.textContent).toContain('Sin logotipo cargado');
  });
});
