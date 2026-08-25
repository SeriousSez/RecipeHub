import { Component, ElementRef, EventEmitter, Input, OnChanges, OnInit, Output, SimpleChanges, ViewChild } from '@angular/core';
import { AbstractControl, UntypedFormBuilder, UntypedFormGroup, Validators } from '@angular/forms';
import { of, forkJoin } from 'rxjs';
import { switchMap } from 'rxjs/operators';
import { Router } from '@angular/router';
import { ImageCroppedEvent } from 'ngx-image-cropper';
import { Ingredient } from 'src/app/recipe/models/ingredient.interface';
import { IngredientService } from 'src/app/recipe/services/ingredient.service';
import { IngredientCreation } from 'src/app/shared/models/ingredient.creation.interface';
import { UserService } from 'src/app/shared/services/user.service';

@Component({
    selector: 'app-ingredient-modal',
    templateUrl: './ingredient.modal.html',
    styleUrls: ['./ingredient.modal.css'],
    standalone: false
})
export class ingredientModal implements OnChanges, OnInit {
    @Input() ingredients: Ingredient[] = [];
    @Input() ingredient: Ingredient | null = null;
    @Input() editMode = false;

    @Output() finish = new EventEmitter();

    public readonly translationFields = [
        { language: 'Danish', control: 'danishName' },
        { language: 'Estonian', control: 'estonianName' },
        { language: 'Turkish', control: 'turkishName' }
    ];

    public ingredientForm: UntypedFormGroup;

    public errors: string = '';
    public isRequesting: boolean = false;
    public submitted: boolean = false;

    public defaultImageUrl: string = "../../assets/images/food.png";
    public imageUrl: string;
    public fileToUpload: File | null;
    public savedOrCanceled: boolean = false;
    public imageChangedEvent: any = '';
    public croppedImage: any = '';
    public showCropOverlay = false;
    public get isEditMode(): boolean { return this.editMode; }

    constructor(private ingredientService: IngredientService, private userService: UserService, private router: Router, private formBuilder: UntypedFormBuilder) { }

    ngOnInit(): void {
        this.imageUrl = this.defaultImageUrl;

        this.ingredientForm = this.formBuilder.group({
            name: [this.ingredient?.name ?? '', Validators.required],
            description: [this.ingredient?.description ?? ''],
            danishName: [''],
            estonianName: [''],
            turkishName: [''],
            imageCaption: ['']
        });
        this.applyIngredientToForm();
    }

    ngOnChanges(changes: SimpleChanges): void {
        if (changes['ingredient'] && this.ingredientForm) {
            this.applyIngredientToForm();
        }
    }

    private applyIngredientToForm(): void {
        this.ingredientForm.patchValue({
            name: this.ingredient?.name ?? '',
            description: this.ingredient?.description ?? '',
            danishName: '',
            estonianName: '',
            turkishName: '',
            imageCaption: this.ingredient?.image?.caption ?? ''
        });
        this.imageUrl = this.ingredient?.image?.url || this.defaultImageUrl;
        this.submitted = false;
        this.errors = '';
        if (this.ingredient) this.loadTranslations(this.ingredient.name);
    }

    private loadTranslations(ingredientName: string): void {
        this.ingredientService.getTranslations(ingredientName).subscribe(translations => {
            this.translationFields.forEach(field => this.ingredientForm.patchValue({
                [field.control]: translations?.[field.language] ?? ''
            }));
        });
    }

    create({ value, valid }: { value: IngredientCreation, valid: boolean }) {
        this.submitted = true;
        this.isRequesting = true;
        this.errors = '';

        if (!this.isEditMode && this.checkForExisting(value)) {
            this.isRequesting = false;
            this.errors = 'This ingredient exists already!';
            return;
        }

        if (this.imageUrl && this.imageUrl !== this.defaultImageUrl) {
            value.image = { url: this.imageUrl, caption: value.imageCaption };
        } else {
            value.image = null;
        }

        if (valid) {
            const request = this.isEditMode
                ? { ...this.ingredient, ...value, language: 'English', image: value.image ?? this.ingredient?.image }
                : { ...value, language: 'English' };
            const saveRequest = this.isEditMode
                ? this.ingredientService.update(request as Ingredient)
                : this.ingredientService.create(request as IngredientCreation);
            saveRequest.pipe(switchMap(() => this.saveTranslations(request.name, value)))
                .subscribe(result => {
                    this.finish.next(this.isEditMode ? request : this.createIngredientModel());
                    this.resetForm();
                    this.router.navigate(['dashboard/ingredients']);
                }, errors => {
                    this.isRequesting = false;
                    this.errors = errors.error;
                });
        }
    }

    private saveTranslations(ingredientName: string, value: IngredientCreation) {
        const requests = this.translationFields
            .map(field => ({ language: field.language, name: (value as any)[field.control] as string }))
            .filter(translation => !!translation.name?.trim())
            .map(translation => this.ingredientService.updateTranslation(ingredientName, translation.language, translation.name.trim()));
        return requests.length > 0 ? forkJoin(requests) : of(null);
    }

    createIngredientModel() {
        var model: Ingredient = {
            name: this.ingredientForm.controls['name'].value,
            description: this.ingredientForm.controls['description'].value,
            language: 'English',
            image: { id: '', url: this.imageUrl || this.defaultImageUrl, caption: this.ingredientForm.controls['imageCaption'].value },
            amount: 0,
            amountType: '',
            created: Date.now().toString()
        }

        return model;
    }

    resetForm() {
        this.ingredientForm.reset();
    }

    cancel() {
        this.finish.next(null);
        this.resetForm();
    }

    checkForExisting(ingredient: IngredientCreation) {
        return this.ingredients.some(i => i.name.toLowerCase() == ingredient.name.toLowerCase());
    }

    handleFileInput(event: any) {
        if (event.target.files.length < 1) {
            this.imageUrl = "";
            this.showCropOverlay = false;
            return;
        }

        this.showCropOverlay = true;
        this.imageChangedEvent = event;
        this.fileToUpload = event.target.files.item(0);

        if (this.fileToUpload == null)
            return

        var reader = new FileReader();
        reader.onload = (event: any) => {
            this.imageUrl = event.target.result;
        }

        reader.readAsDataURL(this.fileToUpload);
    }

    removeImage() {
        this.imageUrl = this.defaultImageUrl;
        this.savedOrCanceled = false;
    }

    cancelImageUpload() {
        this.imageUrl = this.defaultImageUrl;
        this.savedOrCanceled = false;
        this.showCropOverlay = false;
    }

    imageCropped(event: ImageCroppedEvent) {
        if (event.base64 == null) return;

        this.imageUrl = event.base64;
        this.savedOrCanceled = true;
    }
    imageLoaded() {
        // show cropper
    }
    cropperReady() {
        // cropper ready
    }
    loadImageFailed() {
        // show message
    }

    get f(): { [key: string]: AbstractControl } {
        return this.ingredientForm.controls;
    }
}
