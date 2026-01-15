-- Scriptul pentru a modifica rolul unui utilizator din Admin în User

-- 1. Vezi toți utilizatorii și rolurile lor
SELECT Id, Username, Role, CreatedAt FROM Users;

-- 2. Modifică rolul unui utilizator specific (înlocuiește 'nume_utilizator' cu username-ul real)
UPDATE Users 
SET Role = 'User' 
WHERE Username = 'nume_utilizator';

-- 3. Sau modifică după ID (înlocuiește 1 cu ID-ul real)
UPDATE Users 
SET Role = 'User' 
WHERE Id = 1;

-- 4. Verifică modificarea
SELECT Id, Username, Role, CreatedAt FROM Users WHERE Username = 'nume_utilizator';

-- 5. Dacă vrei să setezi un anumit user ca Admin
UPDATE Users 
SET Role = 'Admin' 
WHERE Username = 'nume_utilizator';
