-- seed data:

INSERT INTO crm.events_data (event_id, event_description)
VALUES
(1,'POVEĆANJE CIJENE'),
(2,'SMANJENJE CIJENE'),
(3,'NOVI PROIZVOD'),
(4,'IZLAZ IZ ASORTIMANA');


INSERT INTO crm.retailer_basic_data (retailer_name)
values 
('KONZUM'),
('KTC'),
('LIDL'),
('KAUFLAND'),
('EUROSPIN'),
('DM')

INSERT INTO crm.retailer_business_unit_data (is_active, retailer_id, lookup, filename, street_name, street_number, zip_code, settlement_name)
values
(true, 1, null, 'SUPERMARKET,OSJEČKA 6 33000 VIROVITICA', 'OSJEČKA', '6', '33000', 'VIROVITICA'), --KONZUM
(true, 1, null, 'SUPERMARKET,TRG DR. FRANJE TUĐMANA 8 33000 VIROVITICA', 'TRG DR. FRANJE TUĐMANA', '8', '33000', 'VIROVITICA'), --KONZUM
(true, 2, 'RC%20VIROVITICA%20PJ-46', null, 'VUKOVARSKA', '3A', '33000', 'VIROVITICA'), --KTC
(true, 3, null, 'Supermarket 153_Virovitica_Ul.Stjepana Radića 68_33000_Virovitica', 'Ulica Stjepana Radića', '68', '33000', 'VIROVITICA'), --LIDL
(false, 6, null, 'vlada-oznacavanje-cijena-cijenik', 'Ulica Ote Horvata', '1', '33000', 'VIROVITICA') --DM