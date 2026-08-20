import { Component, ElementRef, EventEmitter, Input, OnInit, Output, ViewChild } from '@angular/core';
import { AbstractControl, UntypedFormBuilder, UntypedFormGroup, Validators } from '@angular/forms';
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
export class ingredientModal implements OnInit {
    @Input() ingredients: Ingredient[] = [];

    @Output() finish = new EventEmitter();

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

    constructor(private ingredientService: IngredientService, private userService: UserService, private router: Router, private formBuilder: UntypedFormBuilder) { }

    ngOnInit(): void {
        this.imageUrl = this.defaultImageUrl;

        this.ingredientForm = this.formBuilder.group({
            name: ['', Validators.required],
            description: ['', Validators.required],
            imageCaption: ['']
        });
    }

    create({ value, valid }: { value: IngredientCreation, valid: boolean }) {
        this.submitted = true;
        this.isRequesting = true;
        this.errors = '';

        if (this.checkForExisting(value)) {
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
            this.ingredientService.create(value)
                .subscribe(result => {
                    this.finish.next(this.createIngredientModel());
                    this.resetForm();
                    this.router.navigate(['dashboard/ingredients']);
                }, errors => {
                    this.isRequesting = false;
                    this.errors = errors.error;
                });
        }
    }

    createIngredientModel() {
        var model: Ingredient = {
            name: this.ingredientForm.controls['name'].value,
            description: this.ingredientForm.controls['description'].value,
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
