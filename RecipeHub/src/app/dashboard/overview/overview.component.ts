import { Component, ElementRef, OnInit, Renderer2, ViewChild } from '@angular/core';
import { v4 as uuidv4 } from 'uuid';
import { ClipboardService } from 'ngx-clipboard';
import { User } from 'src/app/account/models/user.interface';
import { UserRegistration } from 'src/app/shared/models/user.registration.interface';
import { UserService } from 'src/app/shared/services/user.service';

import { DashboardService } from '../services/dashboard.service';
import { DatePipe } from '@angular/common';
import { TranslateService } from '@ngx-translate/core';

@Component({
  selector: 'app-overview',
  templateUrl: './overview.component.html',
  styleUrls: ['./overview.component.css'],
  standalone: false
})
export class OverviewComponent implements OnInit {
  @ViewChild('customerModal') private customerModal: ElementRef;
  @ViewChild('deleteButton') private deleteButton: ElementRef;

  targetUrl: string = '/dashboard/createuser';

  users: User[];
  roles: string[];

  selectedUsers: User[] = [];
  public userSearch: string = '';
  noTestUsers: boolean = true;
  public creatingTestUser = false;
  public deletingSelectedUsers = false;
  public deletingTestUsers = false;
  public showDeleteConfirmation = false;
  public showDeleteTestUsersConfirmation = false;

  public sortSetting: string = 'role';
  public ascending: boolean = true;

  constructor(
    private clipboardApi: ClipboardService,
    private dashboardService: DashboardService,
    private userService: UserService,
    private datepipe: DatePipe,
    private translateService: TranslateService
  ) { }

  ngOnInit() {
    this.getUsers();
    this.getRoles();
  }

  getUsers() {
    this.dashboardService.getUsers()
      .subscribe((users: User[]) => {
        this.users = users;
        this.setFullName();
        this.checkForTestUsers();
        this.sort(this.sortSetting);
      },
        error => {
          //this.notificationService.printErrorMessage(error);
        });
  }

  get filteredUsers(): User[] {
    const search = this.userSearch.trim().toLocaleLowerCase();
    if (!search) return this.users ?? [];

    return (this.users ?? []).filter(user =>
      [user.fullName, user.firstName, user.lastName, user.email, user.userName, user.role]
        .some(value => value?.toLocaleLowerCase().includes(search))
    );
  }

  setFullName() {
    this.users.forEach(user => {
      user.fullName = `${user.firstName} ${user.lastName}`
    });
  }

  getRoles() {
    this.dashboardService.getRoles()
      .subscribe((roles: string[]) => {
        this.roles = roles;
      },
        error => {
          //this.notificationService.printErrorMessage(error);
        });
  }

  createTestUser() {
    if (this.creatingTestUser) return;

    this.creatingTestUser = true;
    var model: UserRegistration = {
      username: uuidv4().toString(),
      email: uuidv4().toString() + "@test.com",
      password: 'Test1!',
      firstName: uuidv4().toString(),
      lastName: '',
      role: 'Test'
    };

    this.userService.register(model, this.targetUrl).subscribe(result => {
      this.users.push(this.createUserModel(model));
      this.setFullName();
      this.checkForTestUsers();
      this.creatingTestUser = false;
    }, errors => {
      this.creatingTestUser = false;
    });
  }

  updateRole(user: User, role: string) {
    user.role = role;

    this.dashboardService.addRole(user).subscribe();
  }

  requestDeleteUsers() {
    if (this.selectedUsers.length === 0) return;
    this.showDeleteConfirmation = true;
  }

  cancelDeleteUsers() {
    this.showDeleteConfirmation = false;
    this.deletingSelectedUsers = false;
  }

  deleteUsers() {
    if (this.selectedUsers.length === 0 || this.deletingSelectedUsers) return;

    this.deletingSelectedUsers = true;
    this.dashboardService.deleteUsers(this.selectedUsers).subscribe((users: User[]) => {
      this.selectedUsers.forEach((user: User) => {
        this.removeUserFromList(user);
      });

      this.selectedUsers = [];
      this.showDeleteConfirmation = false;
      this.deletingSelectedUsers = false;
      this.closeDeleteModal();
    },
      error => {
        this.deletingSelectedUsers = false;
        this.showDeleteConfirmation = false;
        //this.notificationService.printErrorMessage(error);
      });
  }

  requestDeleteTestUsers() {
    if (this.noTestUsers) return;
    this.showDeleteTestUsersConfirmation = true;
  }

  cancelDeleteTestUsers() {
    this.showDeleteTestUsersConfirmation = false;
    this.deletingTestUsers = false;
  }

  deleteTestUsers() {
    if (this.noTestUsers || this.deletingTestUsers) return;

    this.deletingTestUsers = true;
    var testUsers = this.users.filter(u => u.role === 'Test');
    this.dashboardService.deleteUsers(testUsers).subscribe((users: User[]) => {
      testUsers.forEach((user: User) => {
        this.removeUserFromList(user);
      });
      this.showDeleteTestUsersConfirmation = false;
      this.deletingTestUsers = false;
    },
      error => {
        this.deletingTestUsers = false;
        this.showDeleteTestUsersConfirmation = false;
        //this.notificationService.printErrorMessage(error);
      });
  }

  toggleUserSelected(user: User) {
    var index = this.selectedUsers.indexOf(user, 0);
    if (index > -1) {
      this.selectedUsers.splice(index, 1);
    } else {
      this.selectedUsers.push(user);
    }
  }

  removeUserFromList(user: User) {
    var index = this.users.indexOf(user, 0);
    if (index > -1) {
      this.users.splice(index, 1);
    } else {
      this.users.push(user);
    }

    this.checkForTestUsers();
  }

  public closeCustomerModal(user: User) {
    this.users.push(user);
    this.checkForTestUsers();
    this.customerModal.nativeElement.click();
  }

  public get deleteUsersTitle(): string {
    if (this.selectedUsers.length === 0) {
      return `${this.translateService.instant('dashboard.deletePrefix')} ${this.translateService.instant('dashboard.noUsersSelected')}`;
    }

    return `${this.translateService.instant('dashboard.deletePrefix')} ${this.selectedUsers.map(user => user.userName).join(', ')}`;
  }

  public get deleteUsersMessage(): string {
    return this.selectedUsers.length > 0
      ? this.translateService.instant('dashboard.deleteSelectedUsersMessage')
      : this.translateService.instant('dashboard.noUsersSelected');
  }

  public get deleteTestUsersTitle(): string {
    return this.translateService.instant('dashboard.deleteTestUsersButton');
  }

  public get deleteTestUsersMessage(): string {
    return this.translateService.instant('dashboard.deleteTestUsersMessage');
  }

  public closeDeleteModal() {
    this.deleteButton.nativeElement.click();
  }

  checkForTestUsers() {
    this.noTestUsers = this.users.every(x => x.role !== 'Test')
  }

  copyText(text: string) {
    this.clipboardApi.copyFromContent(text)
  }

  displayDateOnly(created: string) {
    return this.datepipe.transform(created, 'dd-MM-yyyy');
  }

  sort(sortSetting: string) {
    if (this.sortSetting != sortSetting) this.ascending = true;
    this.sortSetting = sortSetting;

    switch (sortSetting) {
      case 'fullname':
        this.users.sort((a, b) => this.ascending == true ? a.fullName.localeCompare(b.fullName) : -a.fullName.localeCompare(b.fullName));
        this.ascending = !this.ascending;
        return;
      case 'email':
        this.users.sort((a, b) => this.ascending == true ? a.email.localeCompare(b.email) : -a.email.localeCompare(b.email));
        this.ascending = !this.ascending;
        return;
      case 'username':
        this.users.sort((a, b) => this.ascending == true ? a.userName.localeCompare(b.userName) : -a.userName.localeCompare(b.userName));
        this.ascending = !this.ascending;
        return;
      case 'role':
        this.users.sort((a, b) => this.ascending == true ? a.role.localeCompare(b.role) : -a.role.localeCompare(b.role));
        this.ascending = !this.ascending;
        return;
    }
  }

  createUserModel(user: UserRegistration) {
    var model: User = {
      id: '',
      userName: user.username,
      firstName: user.firstName,
      lastName: user.lastName,
      fullName: '',
      email: user.email,
      role: user.role
    }

    return model;
  }
}
