CREATE TABLE IF NOT EXISTS employee_notification_reads (
    notification_read_id BIGINT AUTO_INCREMENT PRIMARY KEY,
    employee_id INT NOT NULL,
    module_key VARCHAR(50) NOT NULL,
    source_id BIGINT NOT NULL,
    event_at DATETIME NOT NULL,
    read_at DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP,
    UNIQUE KEY uq_employee_notification_read (employee_id, module_key, source_id, event_at),
    KEY idx_employee_notification_reads_employee (employee_id, read_at),
    CONSTRAINT fk_employee_notification_reads_employee
        FOREIGN KEY (employee_id) REFERENCES employees(employee_id)
        ON UPDATE CASCADE
        ON DELETE CASCADE
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4;
