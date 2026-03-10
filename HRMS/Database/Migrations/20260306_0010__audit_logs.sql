CREATE TABLE IF NOT EXISTS audit_logs (
  audit_log_id BIGINT AUTO_INCREMENT PRIMARY KEY,
  acted_by_user_id INT NULL,
  action_code VARCHAR(120) NOT NULL,
  target_type VARCHAR(80) NOT NULL,
  target_id VARCHAR(120) NULL,
  result_status ENUM('SUCCESS','DENIED','FAILED') NOT NULL DEFAULT 'SUCCESS',
  details VARCHAR(1000) NULL,
  created_at DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP,
  KEY idx_audit_action_created (action_code, created_at),
  KEY idx_audit_actor_created (acted_by_user_id, created_at),
  KEY idx_audit_target (target_type, target_id),
  CONSTRAINT fk_audit_logs_actor
    FOREIGN KEY (acted_by_user_id) REFERENCES user_accounts(user_id)
    ON UPDATE CASCADE ON DELETE SET NULL
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4;
