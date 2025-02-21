-- Create the database
CREATE DATABASE IF NOT EXISTS your_project_db;
USE your_project_db;

-- Create halls table
CREATE TABLE Halls (
    Id INT PRIMARY KEY AUTO_INCREMENT,
    Name VARCHAR(50) NOT NULL UNIQUE,
    Capacity INT NOT NULL,
    CreatedAt TIMESTAMP DEFAULT CURRENT_TIMESTAMP
);

-- Create seats table
CREATE TABLE Seats (
    Id INT PRIMARY KEY AUTO_INCREMENT,
    HallId INT NOT NULL,
    RowNumber INT NOT NULL,
    SeatNumber INT NOT NULL,
    CreatedAt TIMESTAMP DEFAULT CURRENT_TIMESTAMP,
    FOREIGN KEY (HallId) REFERENCES Halls(Id) ON DELETE CASCADE,
    CONSTRAINT unique_seat_in_hall UNIQUE (HallId, RowNumber, SeatNumber)
);

-- Create movies table
CREATE TABLE Movies (
    Id INT PRIMARY KEY AUTO_INCREMENT,
    Title VARCHAR(255) NOT NULL,
    Description TEXT,
    DurationMinutes INT NOT NULL,
    ReleaseDate DATE,
    IsActive BOOLEAN DEFAULT TRUE,
    CreatedAt TIMESTAMP DEFAULT CURRENT_TIMESTAMP,
    UpdatedAt TIMESTAMP DEFAULT CURRENT_TIMESTAMP ON UPDATE CURRENT_TIMESTAMP
);

-- Create presentations table
CREATE TABLE Presentations (
    Id INT PRIMARY KEY AUTO_INCREMENT,
    MovieId INT NOT NULL,
    HallId INT NOT NULL,
    StartTime DATETIME NOT NULL,
    EndTime DATETIME NOT NULL,
    Price DECIMAL(10, 2) NOT NULL,
    CreatedAt TIMESTAMP DEFAULT CURRENT_TIMESTAMP,
    UpdatedAt TIMESTAMP DEFAULT CURRENT_TIMESTAMP ON UPDATE CURRENT_TIMESTAMP,
    FOREIGN KEY (MovieId) REFERENCES Movies(Id) ON DELETE CASCADE,
    FOREIGN KEY (HallId) REFERENCES Halls(Id) ON DELETE CASCADE
);

-- Create tickets table
CREATE TABLE Tickets (
    Id INT PRIMARY KEY AUTO_INCREMENT,
    PresentationId INT NOT NULL,
    SeatId INT NOT NULL,
    CustomerName VARCHAR(100),
    CustomerEmail VARCHAR(255),
    PurchaseDate TIMESTAMP DEFAULT CURRENT_TIMESTAMP,
    Status ENUM('Reserved', 'Paid', 'Cancelled') DEFAULT 'Reserved',
    CreatedAt TIMESTAMP DEFAULT CURRENT_TIMESTAMP,
    UpdatedAt TIMESTAMP DEFAULT CURRENT_TIMESTAMP ON UPDATE CURRENT_TIMESTAMP,
    FOREIGN KEY (PresentationId) REFERENCES Presentations(Id) ON DELETE CASCADE,
    FOREIGN KEY (SeatId) REFERENCES Seats(Id) ON DELETE CASCADE,
    CONSTRAINT unique_seat_presentation UNIQUE (PresentationId, SeatId)
);

-- Add indexes
CREATE INDEX idx_presentations_movie ON Presentations(MovieId);
CREATE INDEX idx_presentations_hall ON Presentations(HallId);
CREATE INDEX idx_presentations_datetime ON Presentations(StartTime, EndTime);
CREATE INDEX idx_tickets_presentation ON Tickets(PresentationId);
CREATE INDEX idx_tickets_customer ON Tickets(CustomerEmail);

-- Insert halls
INSERT INTO Halls (Name, Capacity) VALUES
    ('Hall 1', 120),
    ('Hall 2', 120),
    ('Hall 3', 120),
    ('Hall 4', 60),
    ('Hall 5', 50),
    ('Hall 6', 50);

-- Insert seats procedure
DELIMITER //
CREATE PROCEDURE InsertSeats()
BEGIN
    DECLARE hallId INT;
    DECLARE rowNum INT;
    DECLARE seatNum INT;
    
    -- Halls 1-3 (8 rows x 15 seats)
    FOR hallId IN 1..3 DO
        SET rowNum = 1;
        WHILE rowNum <= 8 DO
            SET seatNum = 1;
            WHILE seatNum <= 15 DO
                INSERT INTO Seats (HallId, RowNumber, SeatNumber)
                VALUES (hallId, rowNum, seatNum);
                SET seatNum = seatNum + 1;
            END WHILE;
            SET rowNum = rowNum + 1;
        END WHILE;
    END FOR;
    
    -- Hall 4 (6 rows x 10 seats)
    SET rowNum = 1;
    WHILE rowNum <= 6 DO
        SET seatNum = 1;
        WHILE seatNum <= 10 DO
            INSERT INTO Seats (HallId, RowNumber, SeatNumber)
            VALUES (4, rowNum, seatNum);
            SET seatNum = seatNum + 1;
        END WHILE;
        SET rowNum = rowNum + 1;
    END WHILE;
    
    -- Halls 5-6 (2 rows x 10 seats + 2 rows x 15 seats)
    FOR hallId IN 5..6 DO
        -- First 2 rows with 10 seats
        SET rowNum = 1;
        WHILE rowNum <= 2 DO
            SET seatNum = 1;
            WHILE seatNum <= 10 DO
                INSERT INTO Seats (HallId, RowNumber, SeatNumber)
                VALUES (hallId, rowNum, seatNum);
                SET seatNum = seatNum + 1;
            END WHILE;
            SET rowNum = rowNum + 1;
        END WHILE;
        
        -- Last 2 rows with 15 seats
        SET rowNum = 3;
        WHILE rowNum <= 4 DO
            SET seatNum = 1;
            WHILE seatNum <= 15 DO
                INSERT INTO Seats (HallId, RowNumber, SeatNumber)
                VALUES (hallId, rowNum, seatNum);
                SET seatNum = seatNum + 1;
            END WHILE;
            SET rowNum = rowNum + 1;
        END WHILE;
    END FOR;
END //
DELIMITER ;

-- Execute the procedure to insert all seats
CALL InsertSeats();
DROP PROCEDURE InsertSeats; 