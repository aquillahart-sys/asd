CREATE DATABASE IF NOT EXISTS enrollment_db;
USE enrollment_db;

CREATE TABLE IF NOT EXISTS programs (
    program_id INT NOT NULL AUTO_INCREMENT PRIMARY KEY,
    program_code VARCHAR(20) NOT NULL UNIQUE,
    program_name VARCHAR(100) NOT NULL,
    description VARCHAR(255) NULL,
    status VARCHAR(20) NOT NULL DEFAULT 'Active'
) ENGINE=InnoDB;

CREATE TABLE IF NOT EXISTS students (
    student_id INT NOT NULL AUTO_INCREMENT PRIMARY KEY,
    student_number VARCHAR(30) NOT NULL UNIQUE,
    first_name VARCHAR(100) NOT NULL,
    last_name VARCHAR(100) NOT NULL,
    email VARCHAR(150) NOT NULL,
    program_id INT NULL,
    year_level VARCHAR(20) NOT NULL DEFAULT '1st Year',
    CONSTRAINT fk_students_program
        FOREIGN KEY (program_id) REFERENCES programs(program_id)
) ENGINE=InnoDB;

CREATE TABLE IF NOT EXISTS subjects (
    subject_id INT NOT NULL AUTO_INCREMENT PRIMARY KEY,
    subject_code VARCHAR(30) NOT NULL UNIQUE,
    subject_description VARCHAR(150) NOT NULL,
    units INT NOT NULL
) ENGINE=InnoDB;

CREATE TABLE IF NOT EXISTS subject_offerings (
    offering_id INT NOT NULL AUTO_INCREMENT PRIMARY KEY,
    subject_id INT NOT NULL,
    academic_year VARCHAR(20) NOT NULL DEFAULT '2026-2027',
    semester VARCHAR(20) NOT NULL DEFAULT '1st Semester',
    section VARCHAR(10) NOT NULL DEFAULT 'A',
    schedule VARCHAR(100) NULL,
    instructor VARCHAR(100) NOT NULL DEFAULT 'TBA',
    time_schedule VARCHAR(100) NOT NULL DEFAULT 'TBA',
    status VARCHAR(20) NOT NULL DEFAULT 'Active',
    program_id INT NULL,
    room VARCHAR(50) NOT NULL DEFAULT 'TBA',
    CONSTRAINT fk_offerings_subject
        FOREIGN KEY (subject_id) REFERENCES subjects(subject_id)
) ENGINE=InnoDB;

CREATE TABLE IF NOT EXISTS users (
    user_id INT NOT NULL AUTO_INCREMENT PRIMARY KEY,
    username VARCHAR(50) NOT NULL UNIQUE,
    password_hash VARCHAR(255) NOT NULL,
    password_salt VARCHAR(255) NOT NULL,
    first_name VARCHAR(50) NOT NULL,
    last_name VARCHAR(50) NOT NULL,
    email VARCHAR(100) NOT NULL UNIQUE,
    role VARCHAR(30) NOT NULL DEFAULT 'Administrator',
    status VARCHAR(20) NOT NULL DEFAULT 'Active',
    is_active TINYINT(1) NOT NULL DEFAULT 1,
    created_at DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP
) ENGINE=InnoDB;

ALTER TABLE users
    ADD COLUMN IF NOT EXISTS password_salt VARCHAR(255) NOT NULL DEFAULT '',
    ADD COLUMN IF NOT EXISTS is_active TINYINT(1) NOT NULL DEFAULT 1;

UPDATE users
SET is_active = CASE
    WHEN LOWER(COALESCE(status, 'active')) IN ('active', 'enabled', '1') THEN 1
    ELSE 0
END;

CREATE TABLE IF NOT EXISTS enrollments (
    enrollment_id INT NOT NULL AUTO_INCREMENT PRIMARY KEY,
    student_id INT NOT NULL,
    academic_year VARCHAR(20) NOT NULL,
    semester VARCHAR(20) NOT NULL,
    total_units INT NOT NULL DEFAULT 0,
    tuition_fee DECIMAL(10,2) NOT NULL DEFAULT 0.00,
    miscellaneous_fee DECIMAL(10,2) NOT NULL DEFAULT 0.00,
    other_fees DECIMAL(10,2) NOT NULL DEFAULT 0.00,
    total_assessment DECIMAL(10,2) NOT NULL DEFAULT 0.00,
    required_downpayment DECIMAL(10,2) NOT NULL DEFAULT 0.00,
    status VARCHAR(30) NOT NULL DEFAULT 'Pending',
    enrollment_date DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP,
    CONSTRAINT fk_enrollments_student
        FOREIGN KEY (student_id) REFERENCES students(student_id),
    INDEX ix_enrollments_student_term (student_id, academic_year, semester)
) ENGINE=InnoDB;

CREATE TABLE IF NOT EXISTS enrollment_details (
    enrollment_detail_id INT NOT NULL AUTO_INCREMENT PRIMARY KEY,
    enrollment_id INT NOT NULL,
    subject_offering_id INT NOT NULL,
    subject_id INT NOT NULL,
    units INT NOT NULL,
    amount DECIMAL(10,2) NOT NULL DEFAULT 0.00,
    CONSTRAINT uq_enrollment_offering UNIQUE (enrollment_id, subject_offering_id),
    CONSTRAINT fk_details_enrollment
        FOREIGN KEY (enrollment_id) REFERENCES enrollments(enrollment_id),
    CONSTRAINT fk_details_offering
        FOREIGN KEY (subject_offering_id) REFERENCES subject_offerings(offering_id),
    CONSTRAINT fk_details_subject
        FOREIGN KEY (subject_id) REFERENCES subjects(subject_id)
) ENGINE=InnoDB;

CREATE TABLE IF NOT EXISTS payment_schedules (
    schedule_id INT NOT NULL AUTO_INCREMENT PRIMARY KEY,
    enrollment_id INT NOT NULL,
    installment_name VARCHAR(50) NOT NULL,
    expected_amount DECIMAL(10,2) NOT NULL DEFAULT 0.00,
    amount_paid DECIMAL(10,2) NOT NULL DEFAULT 0.00,
    due_date DATE NOT NULL,
    schedule_status VARCHAR(20) NOT NULL DEFAULT 'Unpaid',
    CONSTRAINT fk_schedules_enrollment
        FOREIGN KEY (enrollment_id) REFERENCES enrollments(enrollment_id)
) ENGINE=InnoDB;

CREATE TABLE IF NOT EXISTS payments (
    payment_id INT NOT NULL AUTO_INCREMENT PRIMARY KEY,
    enrollment_id INT NOT NULL,
    schedule_id INT NULL,
    receipt_number VARCHAR(50) NOT NULL UNIQUE,
    payment_date DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP,
    payment_type VARCHAR(30) NOT NULL,
    payment_method VARCHAR(50) NOT NULL DEFAULT 'Cash',
    amount_paid DECIMAL(10,2) NOT NULL DEFAULT 0.00,
    processed_by INT NULL,
    CONSTRAINT fk_payments_enrollment
        FOREIGN KEY (enrollment_id) REFERENCES enrollments(enrollment_id)
) ENGINE=InnoDB;

ALTER TABLE payments
    ADD COLUMN IF NOT EXISTS schedule_id INT NULL,
    ADD COLUMN IF NOT EXISTS processed_by INT NULL;

CREATE TABLE IF NOT EXISTS audit_logs (
    audit_id BIGINT NOT NULL AUTO_INCREMENT PRIMARY KEY,
    user_id INT NULL,
    action_type VARCHAR(60) NOT NULL,
    entity_type VARCHAR(60) NULL,
    entity_id VARCHAR(60) NULL,
    details VARCHAR(500) NULL,
    created_at DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP,
    CONSTRAINT fk_audit_user
        FOREIGN KEY (user_id) REFERENCES users(user_id),
    INDEX ix_audit_created (created_at, audit_id)
) ENGINE=InnoDB;
