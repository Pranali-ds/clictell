/**
 * @NApiVersion 2.x
 * @NScriptType Restlet
 * @NAuthor Rahman(Dhruvsoft)
 * The Requirement is push the invoice from external allication to NetSuite by using Rest API EndPoint
 */
define(['N/record', 'N/error', 'N/log', 'N/search','N/runtime', 'N/email'], function(record, error, log, search, runtime,email) {
	function createInvoice(data) {
		try {
			log.debug("Incoming Data", JSON.stringify(data));
            // var getinvoice_type=data.invoice_type;
            // log.debug("getinvoice_type",getinvoice_type);
			try {
				var customer_internal_id = getCustomerDetail(data.customerId);
				if (!customer_internal_id) {
					log.error("Invalid customer" + customer_internal_id);
					return; // Skip if customer is invalid
				}
			} catch (e) {
				log.error("Error in customer function", e.message);
			}
			try {
				var invoice_type_rec_id = getinvoice_type(data.invoice_type);
              log.debug("invoice_type_rec_id",invoice_type_rec_id);
				if (!invoice_type_rec_id) {
					log.error("Invoice Record Type Id" + invoice_type_rec_id);
					return; // Skip if customer is invalid
				}
			} catch (e) {
				log.error("Error in customer function", e.message);
			}
			// Create new invoice record
            //invoice details for Lease Billing Invoice
 if(invoice_type_rec_id=='1'){
          var customformId=126;
         // if(customform=='125'){  
			var invoice = record.create({
				type: record.Type.INVOICE,
				isDynamic: true
			});
			log.debug("New invoice created", invoice);


            invoice.setValue({
				fieldId: 'customform',
				value: customformId // Set the form to the specified form
			});

			// Set the customer field
			if (data.invoiceNumber) {
				var invoiceNo=invoice.setValue({
					fieldId: 'tranid',
					value: data.invoiceNumber // Ensure the customer ID exists in NetSuite
				});
			}
			if (customer_internal_id) {
				var set_cust = invoice.setValue({
					fieldId: 'entity',
					value: customer_internal_id // Ensure the customer ID exists in NetSuite
				});
			}
			log.debug("Customer set", set_cust);

			if (data.invoiceDate) {
				invoice.setValue({
					fieldId: 'trandate',
					value: data.invoiceDate
				});
			}
			if (data.dueDate) {
				invoice.setValue({
					fieldId: 'duedate',
					value: data.dueDate
				});
			}
			if (data.sourceappcode) {
				invoice.setValue({
					fieldId: 'custbody_source_application_code',
					value: data.sourceappcode
				});
			}
			if (data.memoHeader) {
				invoice.setValue({
					fieldId: 'memo',
					value: data.memoHeader
				});
			}
			
             if(invoice_type_rec_id){
                invoice.setValue({
                    fieldId:'custbody_invoice_type',
                    value:invoice_type_rec_id
                });
             }
			 if(data.fvDateCreated){
                invoice.setValue({
                    fieldId:'custbody_fv_date_created',
                    value:data.fvDateCreated
                });
             }
			// Add items to the invoice
			if (data.items && Array.isArray(data.items) && data.items.length > 0) {
				data.items.forEach(function(item, index) {
					// Validate item
					if (!item.item_cd || !item.quantity || !item.rate) {
						throw error.create({
							name: 'INVALID_ITEM',
							message: 'Item name, quantity, and rate are required for item at index ' + index
						});
					}
					var itemInternalId = getItemDetails(item.item_cd);
					if (!itemInternalId) {
						throw error.create({
							name: 'INVALID_ITEM',
							message: 'Item not found: ' + item.item_cd
						});
					}
					try {
						var cost_center_id = getLocation(item.costCenter);
						if (!cost_center_id) {
							log.debug("invalid cost_center_id", +cost_center_id);
						}
					} catch (e) {
						log.error("Error in cost_center_ id call", e.message);
					}
					invoice.selectNewLine({
						sublistId: 'item'
					});
					// Set item details
					//  itemInternalId.forEach(function(itemInternalId) {
					invoice.setCurrentSublistValue({
						sublistId: 'item',
						fieldId: 'item',
						value: itemInternalId
					});
					//  });
					invoice.setCurrentSublistValue({
						sublistId: 'item',
						fieldId: 'quantity',
						value: item.quantity
					});
				if (item.rate) {
					invoice.setCurrentSublistValue({
						sublistId: 'item',
						fieldId: 'rate',
						value: item.rate
					});
				}
                // Convert charge and tax to numeric values (default to 0 if missing)
var chargeAmount = parseFloat(item.charge_amt) || 0;
var taxAmount = parseFloat(item.tax_amount) || 0;

// Set custom charge amount field
if (chargeAmount !== 0) {
    invoice.setCurrentSublistValue({
        sublistId: 'item',
        fieldId: 'custcol_charge_amount',
        value: chargeAmount
    });
}
// Set tax amount field - replace 'cust_tax_field' with your actual Field ID
if (taxAmount !== 0) {
    invoice.setCurrentSublistValue({
        sublistId: 'item',
        fieldId: 'custcol_tax_amount',
        value: taxAmount
    });
}
// Set total amount (charge + tax)
var totalAmount = chargeAmount + taxAmount;
invoice.setCurrentSublistValue({
    sublistId: 'item',
    fieldId: 'amount',
    value: totalAmount
});
					if (item.itemmemo) {
						invoice.setCurrentSublistValue({
							sublistId: 'item',
							fieldId: 'custcol_line_item_memo',
							value: item.itemmemo
						});
					}
				
					// Set location if provided
					if (cost_center_id) {
						var set_location = invoice.setCurrentSublistValue({
							sublistId: 'item',
							fieldId: 'location',
							value: cost_center_id
						});
						log.debug("set_location", set_location);
					}
					
				
				
					invoice.commitLine({
						sublistId: 'item'
					});
				});
			}
            // Save the invoice record
			var invoiceId = invoice.save({
				enableSourcing: true,
				ignoreMandatoryFields: true
			});
			log.debug("newly created invoiceId", invoiceId);
			// Send a success email to the user
var currentUser = runtime.getCurrentUser();
var userEmail = currentUser.email; // Get the current user's email address
email.send({
	author: currentUser.id, // System user
	recipients: userEmail,
	subject: '---Invoice Created Successfully---',
	body: 'Your invoice has been created successfully. Invoice ID: ' + invoiceId + '. Invoice Number: ' + data.invoiceNumber
});

			//invoice Type - TOLL Rebil Pool invoice 
        }else if(invoice_type_rec_id=='2'){
            var customformId=127;
            // if(customform=='125'){  
               var invoice = record.create({
                   type: record.Type.INVOICE,
                   isDynamic: true
               });
               log.debug("New invoice created", invoice);
               invoice.setValue({
                   fieldId: 'customform',
                   value: customformId // Set the form to the specified form
               });
   
               // Set the customer field
               if (data.invoiceNumber) {
                   var invoiceNo=invoice.setValue({
                       fieldId: 'tranid',
                       value: data.invoiceNumber // Ensure the customer ID exists in NetSuite
                   });
               }
               if (customer_internal_id) {
                   var set_cust = invoice.setValue({
                       fieldId: 'entity',
                       value: customer_internal_id // Ensure the customer ID exists in NetSuite
                   });
               }
               log.debug("Customer set", set_cust);
               if (data.invoiceDate) {
                   invoice.setValue({
                       fieldId: 'trandate',
                       value: data.invoiceDate
                   });
               }
               if (data.dueDate) {
                   invoice.setValue({
                       fieldId: 'duedate',
                       value: data.dueDate
                   });
               }
               if (data.sourceappcode) {
                   invoice.setValue({
                       fieldId: 'custbody_source_application_code',
                       value: data.sourceappcode
                   });
               }
               if (data.memoHeader) {
                   invoice.setValue({
                       fieldId: 'memo',
                       value: data.memoHeader
                   });
               }
			   
			    if (data.fvDueDate) {
                   invoice.setValue({
                       fieldId: 'custbody_fv_due_date',
                       value: data.fvDueDate
                   });
               }
              if(invoice_type_rec_id){
                invoice.setValue({
                    fieldId:'custbody_invoice_type',
                    value:invoice_type_rec_id
                });
             }
               // Add items to the invoice
               if (data.items && Array.isArray(data.items) && data.items.length > 0) {
                   data.items.forEach(function(item, index) {
                       // Validate item
                       if (!item.item_cd || !item.quantity || !item.rate) {
                           throw error.create({
                               name: 'INVALID_ITEM',
                               message: 'Item name, quantity, and rate are required for item at index ' + index
                           });
                       }
                       var itemInternalId = getItemDetails(item.item_cd);
                       if (!itemInternalId) {
                           throw error.create({
                               name: 'INVALID_ITEM',
                               message: 'Item not found: ' + item.item_cd
                           });
                       }
                       try {
                           var cost_center_id = getLocation(item.costCenter);
                           if (!cost_center_id) {
                               log.debug("invalid cost_center_id", +cost_center_id);
                           }
                       } catch (e) {
                           log.error("Error in cost_center_ id call", e.message);
                       }
                       invoice.selectNewLine({
                           sublistId: 'item'
                       });
                       // Set item details
                       //  itemInternalId.forEach(function(itemInternalId) {
                       invoice.setCurrentSublistValue({
                           sublistId: 'item',
                           fieldId: 'item',
                           value: itemInternalId
                       });
                       //  });
                       invoice.setCurrentSublistValue({
                           sublistId: 'item',
                           fieldId: 'quantity',
                           value: item.quantity
                       });
					   if(item.rate){
                       invoice.setCurrentSublistValue({
                           sublistId: 'item',
                           fieldId: 'rate',
                           value: item.rate
                       });
					   }
 // Convert charge and tax to numeric values (default to 0 if missing)
var chargeAmount = parseFloat(item.charge_amt) || 0;
var taxAmount = parseFloat(item.tax_amount) || 0;

// Set custom charge amount field
if (chargeAmount !== 0) {
    invoice.setCurrentSublistValue({
        sublistId: 'item',
        fieldId: 'custcol_charge_amount',
        value: chargeAmount
    });
}
// Set tax amount field - replace 'cust_tax_field' with your actual Field ID
if (taxAmount !== 0) {
    invoice.setCurrentSublistValue({
        sublistId: 'item',
        fieldId: 'custcol_tax_amount',
        value: taxAmount
    });
}
// Set total amount (charge + tax)
var totalAmount = chargeAmount + taxAmount;
invoice.setCurrentSublistValue({
    sublistId: 'item',
    fieldId: 'amount',
    value: totalAmount
});
if (item.itemmemo) {
         invoice.setCurrentSublistValue({
         sublistId: 'item',
         fieldId: 'custcol_line_item_memo',
        value: item.itemmemo
        });
         }
         if (item.chassisId) {
        invoice.setCurrentSublistValue({
      sublistId: 'item',
        fieldId: 'custcol_chassis_id',
         value: item.chassisId
              });
            }
                       // Set location if provided
                       if (cost_center_id) {
                           var set_location = invoice.setCurrentSublistValue({
                               sublistId: 'item',
                               fieldId: 'location',
                               value: cost_center_id
                           });
                           log.debug("set_location", set_location);
                       }
                    
                       invoice.commitLine({
                           sublistId: 'item'
                       });
                   });
               }
               // Save the invoice record
               var invoiceId = invoice.save({
                   enableSourcing: true,
                   ignoreMandatoryFields: true
               });
               log.debug("newly created invoiceId", invoiceId); 
			   // Send a success email to the user
var currentUser = runtime.getCurrentUser();
var userEmail = currentUser.email; // Get the current user's email address
email.send({
	author: currentUser.id, // System user
	recipients: userEmail,
	subject: '---Invoice Created Successfully---',
	body: 'Your invoice has been created successfully. Invoice ID: ' + invoiceId + '. Invoice Number: ' + data.invoiceNumber
});

			   //Invoice Type TOLL Rebil -Retails     
			     }else if(invoice_type_rec_id=='3'){
            var customformId=127;
            // if(customform=='125'){  
               var invoice = record.create({
                   type: record.Type.INVOICE,
                   isDynamic: true
               });
               log.debug("New invoice created", invoice);
   
   
               invoice.setValue({
                   fieldId: 'customform',
                   value: customformId // Set the form to the specified form
               });
   
               // Set the customer field
               if (data.invoiceNumber) {
                   var invoiceNo=invoice.setValue({
                       fieldId: 'tranid',
                       value: data.invoiceNumber // Ensure the customer ID exists in NetSuite
                   });
               }
               if (customer_internal_id) {
                   var set_cust = invoice.setValue({
                       fieldId: 'entity',
                       value: customer_internal_id // Ensure the customer ID exists in NetSuite
                   });
               }
               log.debug("Customer set", set_cust);
               if (data.invoiceDate) {
                   invoice.setValue({
                       fieldId: 'trandate',
                       value: data.invoiceDate
                   });
               }
               if (data.dueDate) {
                   invoice.setValue({
                       fieldId: 'duedate',
                       value: data.dueDate
                   });
               }
               if (data.sourceappcode) {
                   invoice.setValue({
                       fieldId: 'custbody_source_application_code',
                       value: data.sourceappcode
                   });
               }
               if (data.memoHeader) {
                   invoice.setValue({
                       fieldId: 'memo',
                       value: data.memoHeader
                   });
               }
                 if(invoice_type_rec_id){
                invoice.setValue({
                    fieldId:'custbody_invoice_type',
                    value:invoice_type_rec_id
                });
             }
               // Add items to the invoice
               if (data.items && Array.isArray(data.items) && data.items.length > 0) {
                   data.items.forEach(function(item, index) {
                       // Validate item
                       if (!item.item_cd || !item.quantity || !item.rate) {
                           throw error.create({
                               name: 'INVALID_ITEM',
                               message: 'Item name, quantity, and rate are required for item at index ' + index
                           });
                       }
                       var itemInternalId = getItemDetails(item.item_cd);
                       if (!itemInternalId) {
                           throw error.create({
                               name: 'INVALID_ITEM',
                               message: 'Item not found: ' + item.item_cd
                           });
                       }
                       try {
                           var cost_center_id = getLocation(item.costCenter);
                           if (!cost_center_id) {
                               log.debug("invalid cost_center_id", +cost_center_id);
                           }
                       } catch (e) {
                           log.error("Error in cost_center_ id call", e.message);
                       }
                       invoice.selectNewLine({
                           sublistId: 'item'
                       });
                       // Set item details
                       //  itemInternalId.forEach(function(itemInternalId) {
                       invoice.setCurrentSublistValue({
                           sublistId: 'item',
                           fieldId: 'item',
                           value: itemInternalId
                       });
                       //  });
                       invoice.setCurrentSublistValue({
                           sublistId: 'item',
                           fieldId: 'quantity',
                           value: item.quantity
                       });
                       invoice.setCurrentSublistValue({
                           sublistId: 'item',
                           fieldId: 'rate',
                           value: item.rate
                       });
                      // Convert charge and tax to numeric values (default to 0 if missing)
var chargeAmount = parseFloat(item.charge_amt) || 0;
var taxAmount = parseFloat(item.tax_amount) || 0;

// Set custom charge amount field
if (chargeAmount !== 0) {
    invoice.setCurrentSublistValue({
        sublistId: 'item',
        fieldId: 'custcol_charge_amount',
        value: chargeAmount
    });
}
// Set tax amount field - replace 'cust_tax_field' with your actual Field ID
if (taxAmount !== 0) {
    invoice.setCurrentSublistValue({
        sublistId: 'item',
        fieldId: 'custcol_tax_amount',
        value: taxAmount
    });
}
// Set total amount (charge + tax)
var totalAmount = chargeAmount + taxAmount;
invoice.setCurrentSublistValue({
    sublistId: 'item',
    fieldId: 'amount',
    value: totalAmount
});
if (item.itemmemo) {
         invoice.setCurrentSublistValue({
         sublistId: 'item',
         fieldId: 'custcol_line_item_memo',
        value: item.itemmemo
        });
         }
         if (item.chassisId) {
        invoice.setCurrentSublistValue({
      sublistId: 'item',
        fieldId: 'custcol_chassis_id',
         value: item.chassisId
              });
            }
                       // Set location if provided
                       if (cost_center_id) {
                           var set_location = invoice.setCurrentSublistValue({
                               sublistId: 'item',
                               fieldId: 'location',
                               value: cost_center_id
                           });
                           log.debug("set_location", set_location);
                       }
                       if (item.itemmemo) {
                           invoice.setCurrentSublistValue({
                               sublistId: 'item',
                               fieldId: 'custcol_line_item_memo',
                               value: item.itemmemo
                           });
                       }
                       // Set location if provided
                       if (cost_center_id) {
                           var set_location = invoice.setCurrentSublistValue({
                               sublistId: 'item',
                               fieldId: 'location',
                               value: cost_center_id
                           });
                           log.debug("set_location", set_location);
                       }
                       invoice.commitLine({
                           sublistId: 'item'
                       });
                   });
               }
               // Save the invoice record
               var invoiceId = invoice.save({
                   enableSourcing: true,
                   ignoreMandatoryFields: true
               });
               log.debug("newly created invoiceId", invoiceId);
			   // Send a success email to the user
var currentUser = runtime.getCurrentUser();
var userEmail = currentUser.email; // Get the current user's email address
email.send({
	author: 1221733, // System user
	recipients: userEmail,
	subject: '---Invoice Created Successfully---',
	body: 'Your invoice has been created successfully. Invoice ID: ' + invoiceId + '. Invoice Number: ' + data.invoiceNumber
});
    
			   //invoice Type Citation
			       }else if(invoice_type_rec_id=='4'){
            var customformId=127;
            // if(customform=='125'){  
               var invoice = record.create({
                   type: record.Type.INVOICE,
                   isDynamic: true
               });
               log.debug("New invoice created", invoice);
   
   
               invoice.setValue({
                   fieldId: 'customform',
                   value: customformId // Set the form to the specified form
               });
   
               // Set the customer field
               if (data.invoiceNumber) {
                   var invoiceNo=invoice.setValue({
                       fieldId: 'tranid',
                       value: data.invoiceNumber // Ensure the customer ID exists in NetSuite
                   });
               }
               if (customer_internal_id) {
                   var set_cust = invoice.setValue({
                       fieldId: 'entity',
                       value: customer_internal_id // Ensure the customer ID exists in NetSuite
                   });
               }
               log.debug("Customer set", set_cust);
   
             
               if (data.invoiceDate) {
                   invoice.setValue({
                       fieldId: 'trandate',
                       value: data.invoiceDate
                   });
               }
               if (data.dueDate) {
                   invoice.setValue({
                       fieldId: 'duedate',
                       value: data.dueDate
                   });
               }
               if (data.sourceappcode) {
                   invoice.setValue({
                       fieldId: 'custbody_source_application_code',
                       value: data.sourceappcode
                   });
               }
               if (data.memoHeader) {
                   invoice.setValue({
                       fieldId: 'memo',
                       value: data.memoHeader
                   });
               }
//changes
                if(invoice_type_rec_id){
                invoice.setValue({
                    fieldId:'custbody_invoice_type',
                    value:invoice_type_rec_id
                });
             }
                  // Add items to the invoice
               if (data.items && Array.isArray(data.items) && data.items.length > 0) {
                   data.items.forEach(function(item, index) {
                       // Validate item
                       if (!item.item_cd || !item.quantity || !item.rate) {
                           throw error.create({
                               name: 'INVALID_ITEM',
                               message: 'Item name, quantity, and rate are required for item at index ' + index
                           });
                       }
                       var itemInternalId = getItemDetails(item.item_cd);
                       if (!itemInternalId) {
                           throw error.create({
                               name: 'INVALID_ITEM',
                               message: 'Item not found: ' + item.item_cd
                           });
                       }
                       try {
                           var cost_center_id = getLocation(item.costCenter);
                           if (!cost_center_id) {
                               log.debug("invalid cost_center_id", +cost_center_id);
                           }
                       } catch (e) {
                           log.error("Error in cost_center_ id call", e.message);
                       }
                       invoice.selectNewLine({
                           sublistId: 'item'
                       });
                       // Set item details
                       //  itemInternalId.forEach(function(itemInternalId) {
                       invoice.setCurrentSublistValue({
                           sublistId: 'item',
                           fieldId: 'item',
                           value: itemInternalId
                       });
                       //  });
                       invoice.setCurrentSublistValue({
                           sublistId: 'item',
                           fieldId: 'quantity',
                           value: item.quantity
                       });
                       invoice.setCurrentSublistValue({
                           sublistId: 'item',
                           fieldId: 'rate',
                           value: item.rate
                       });
                      // Convert charge and tax to numeric values (default to 0 if missing)
var chargeAmount = parseFloat(item.charge_amt) || 0;
var taxAmount = parseFloat(item.tax_amount) || 0;

// Set custom charge amount field
if (chargeAmount !== 0) {
    invoice.setCurrentSublistValue({
        sublistId: 'item',
        fieldId: 'custcol_charge_amount',
        value: chargeAmount
    });
}
// Set tax amount field - replace 'cust_tax_field' with your actual Field ID
if (taxAmount !== 0) {
    invoice.setCurrentSublistValue({
        sublistId: 'item',
        fieldId: 'custcol_tax_amount',
        value: taxAmount
    });
}
// Set total amount (charge + tax)
var totalAmount = chargeAmount + taxAmount;
invoice.setCurrentSublistValue({
    sublistId: 'item',
    fieldId: 'amount',
    value: totalAmount
});
if (item.itemmemo) {
         invoice.setCurrentSublistValue({
         sublistId: 'item',
         fieldId: 'custcol_line_item_memo',
        value: item.itemmemo
        });
         }
         if (item.chassisId) {
        invoice.setCurrentSublistValue({
      sublistId: 'item',
        fieldId: 'custcol_chassis_id',
         value: item.chassisId
              });
            }
                       // Set location if provided
                       if (cost_center_id) {
                           var set_location = invoice.setCurrentSublistValue({
                               sublistId: 'item',
                               fieldId: 'location',
                               value: cost_center_id
                           });
                           log.debug("set_location", set_location);
                       }
                       if (item.itemmemo) {
                           invoice.setCurrentSublistValue({
                               sublistId: 'item',
                               fieldId: 'custcol_line_item_memo',
                               value: item.itemmemo
                           });
                       }
                       // Set location if provided
                       if (cost_center_id) {
                           var set_location = invoice.setCurrentSublistValue({
                               sublistId: 'item',
                               fieldId: 'location',
                               value: cost_center_id
                           });
                           log.debug("set_location", set_location);
                       }
                       invoice.commitLine({
                           sublistId: 'item'
                       });
                   });
               }
               // Save the invoice record
               var invoiceId = invoice.save({
                   enableSourcing: true,
                   ignoreMandatoryFields: true
               });
               log.debug("newly created invoiceId", invoiceId);
			   // Send a success email to the user
var currentUser = runtime.getCurrentUser();
var userEmail = currentUser.email; // Get the current user's email address
email.send({
	author: 1221733, // System user
	recipients: userEmail,
	subject: '---Invoice Created Successfully---',
	body: 'Your invoice has been created successfully. Invoice ID: ' + invoiceId + '. Invoice Number: ' + data.invoiceNumber
});
 
			   // invoice Type--- Intrest invoice    
			      }else if(invoice_type_rec_id=='5'){
            var customformId=127;
            // if(customform=='125'){  
               var invoice = record.create({
                   type: record.Type.INVOICE,
                   isDynamic: true
               });
               log.debug("New invoice created", invoice);
   
   
               invoice.setValue({
                   fieldId: 'customform',
                   value: customformId // Set the form to the specified form
               });
   
               // Set the customer field
               if (data.invoiceNumber) {
                   var invoiceNo=invoice.setValue({
                       fieldId: 'tranid',
                       value: data.invoiceNumber // Ensure the customer ID exists in NetSuite
                   });
               }
               if (customer_internal_id) {
                   var set_cust = invoice.setValue({
                       fieldId: 'entity',
                       value: customer_internal_id // Ensure the customer ID exists in NetSuite
                   });
               }
               log.debug("Customer set", set_cust);

               if (data.invoiceDate) {
                   invoice.setValue({
                       fieldId: 'trandate',
                       value: data.invoiceDate
                   });
               }
               if (data.dueDate) {
                   invoice.setValue({
                       fieldId: 'duedate',
                       value: data.dueDate
                   });
               }
               if (data.sourceappcode) {
                   invoice.setValue({
                       fieldId: 'custbody_source_application_code',
                       value: data.sourceappcode
                   });
               }
               if (data.memoHeader) {
                   invoice.setValue({
                       fieldId: 'memo',
                       value: data.memoHeader
                   });
               }
                 if(invoice_type_rec_id){
                invoice.setValue({
                    fieldId:'custbody_invoice_type',
                    value:invoice_type_rec_id
                });
             }
         // Add items to the invoice
               if (data.items && Array.isArray(data.items) && data.items.length > 0) {
                   data.items.forEach(function(item, index) {
                       // Validate item
                       if (!item.item_cd || !item.quantity || !item.rate) {
                           throw error.create({
                               name: 'INVALID_ITEM',
                               message: 'Item name, quantity, and rate are required for item at index ' + index
                           });
                       }
                       var itemInternalId = getItemDetails(item.item_cd);
                       if (!itemInternalId) {
                           throw error.create({
                               name: 'INVALID_ITEM',
                               message: 'Item not found: ' + item.item_cd
                           });
                       }
                       try {
                           var cost_center_id = getLocation(item.costCenter);
                           if (!cost_center_id) {
                               log.debug("invalid cost_center_id", +cost_center_id);
                           }
                       } catch (e) {
                           log.error("Error in cost_center_ id call", e.message);
                       }
                       invoice.selectNewLine({
                           sublistId: 'item'
                       });
                       // Set item details
                       //  itemInternalId.forEach(function(itemInternalId) {
                       invoice.setCurrentSublistValue({
                           sublistId: 'item',
                           fieldId: 'item',
                           value: itemInternalId
                       });
                       //  });
                       invoice.setCurrentSublistValue({
                           sublistId: 'item',
                           fieldId: 'quantity',
                           value: item.quantity
                       });
                       invoice.setCurrentSublistValue({
                           sublistId: 'item',
                           fieldId: 'rate',
                           value: item.rate
                       });
                      // Convert charge and tax to numeric values (default to 0 if missing)
var chargeAmount = parseFloat(item.charge_amt) || 0;
var taxAmount = parseFloat(item.tax_amount) || 0;

// Set custom charge amount field
if (chargeAmount !== 0) {
    invoice.setCurrentSublistValue({
        sublistId: 'item',
        fieldId: 'custcol_charge_amount',
        value: chargeAmount
    });
}
// Set tax amount field - replace 'cust_tax_field' with your actual Field ID
if (taxAmount !== 0) {
    invoice.setCurrentSublistValue({
        sublistId: 'item',
        fieldId: 'custcol_tax_amount',
        value: taxAmount
    });
}
// Set total amount (charge + tax)
var totalAmount = chargeAmount + taxAmount;
invoice.setCurrentSublistValue({
    sublistId: 'item',
    fieldId: 'amount',
    value: totalAmount
});
if (item.itemmemo) {
         invoice.setCurrentSublistValue({
         sublistId: 'item',
         fieldId: 'custcol_line_item_memo',
        value: item.itemmemo
        });
         }
         if (item.chassisId) {
        invoice.setCurrentSublistValue({
      sublistId: 'item',
        fieldId: 'custcol_chassis_id',
         value: item.chassisId
              });
            }
                       // Set location if provided
                       if (cost_center_id) {
                           var set_location = invoice.setCurrentSublistValue({
                               sublistId: 'item',
                               fieldId: 'location',
                               value: cost_center_id
                           });
                           log.debug("set_location", set_location);
                       }
                       if (item.itemmemo) {
                           invoice.setCurrentSublistValue({
                               sublistId: 'item',
                               fieldId: 'custcol_line_item_memo',
                               value: item.itemmemo
                           });
                       }
                       // Set location if provided
                       if (cost_center_id) {
                           var set_location = invoice.setCurrentSublistValue({
                               sublistId: 'item',
                               fieldId: 'location',
                               value: cost_center_id
                           });
                           log.debug("set_location", set_location);
                       }
                       invoice.commitLine({
                           sublistId: 'item'
                       });
                   });
               }
               // Save the invoice record
               var invoiceId = invoice.save({
                   enableSourcing: true,
                   ignoreMandatoryFields: true
               });
               log.debug("newly created invoiceId", invoiceId);
			   // Send a success email to the user
var currentUser = runtime.getCurrentUser();
var userEmail = currentUser.email; // Get the current user's email address
email.send({
	author: 1221733, // System user
	recipients: userEmail,
	subject: '---Invoice Created Successfully---',
	body: 'Your invoice has been created successfully. Invoice ID: ' + invoiceId + '. Invoice Number: ' + data.invoiceNumber
});

			   //TOLL Citation Invoice
			           }else if(invoice_type_rec_id=='6'){
            var customformId=127;
            // if(customform=='125'){  
               var invoice = record.create({
                   type: record.Type.INVOICE,
                   isDynamic: true
               });
               log.debug("New invoice created", invoice);
   
   
               invoice.setValue({
                   fieldId: 'customform',
                   value: customformId // Set the form to the specified form
               });
   
               // Set the customer field
               if (data.invoiceNumber) {
                   var invoiceNo=invoice.setValue({
                       fieldId: 'tranid',
                       value: data.invoiceNumber // Ensure the customer ID exists in NetSuite
                   });
               }
               if (customer_internal_id) {
                   var set_cust = invoice.setValue({
                       fieldId: 'entity',
                       value: customer_internal_id // Ensure the customer ID exists in NetSuite
                   });
               }
               log.debug("Customer set", set_cust);
               if (data.invoiceDate) {
                   invoice.setValue({
                       fieldId: 'trandate',
                       value: data.invoiceDate
                   });
               }
               if (data.dueDate) {
                   invoice.setValue({
                       fieldId: 'duedate',
                       value: data.dueDate
                   });
               }
               if (data.sourceappcode) {
                   invoice.setValue({
                       fieldId: 'custbody_source_application_code',
                       value: data.sourceappcode
                   });
               }
               if (data.memoHeader) {
                   invoice.setValue({
                       fieldId: 'memo',
                       value: data.memoHeader
                   });
               }
              if(invoice_type_rec_id){
                invoice.setValue({
                    fieldId:'custbody_invoice_type',
                    value:invoice_type_rec_id
                });
             }
               // Add items to the invoice
               if (data.items && Array.isArray(data.items) && data.items.length > 0) {
                   data.items.forEach(function(item, index) {
                       // Validate item
                       if (!item.item_cd || !item.quantity || !item.rate) {
                           throw error.create({
                               name: 'INVALID_ITEM',
                               message: 'Item name, quantity, and rate are required for item at index ' + index
                           });
                       }
                       var itemInternalId = getItemDetails(item.item_cd);
                       if (!itemInternalId) {
                           throw error.create({
                               name: 'INVALID_ITEM',
                               message: 'Item not found: ' + item.item_cd
                           });
                       }
                       try {
                           var cost_center_id = getLocation(item.costCenter);
                           if (!cost_center_id) {
                               log.debug("invalid cost_center_id", +cost_center_id);
                           }
                       } catch (e) {
                           log.error("Error in cost_center_ id call", e.message);
                       }
                       invoice.selectNewLine({
                           sublistId: 'item'
                       });
                       // Set item details
                       //  itemInternalId.forEach(function(itemInternalId) {
                       invoice.setCurrentSublistValue({
                           sublistId: 'item',
                           fieldId: 'item',
                           value: itemInternalId
                       });
                       //  });
                       invoice.setCurrentSublistValue({
                           sublistId: 'item',
                           fieldId: 'quantity',
                           value: item.quantity
                       });
                       invoice.setCurrentSublistValue({
                           sublistId: 'item',
                           fieldId: 'rate',
                           value: item.rate
                       });
                      // Convert charge and tax to numeric values (default to 0 if missing)
var chargeAmount = parseFloat(item.charge_amt) || 0;
var taxAmount = parseFloat(item.tax_amount) || 0;

// Set custom charge amount field
if (chargeAmount !== 0) {
    invoice.setCurrentSublistValue({
        sublistId: 'item',
        fieldId: 'custcol_charge_amount',
        value: chargeAmount
    });
}
// Set tax amount field - replace 'cust_tax_field' with your actual Field ID
if (taxAmount !== 0) {
    invoice.setCurrentSublistValue({
        sublistId: 'item',
        fieldId: 'custcol_tax_amount',
        value: taxAmount
    });
}
// Set total amount (charge + tax)
var totalAmount = chargeAmount + taxAmount;
invoice.setCurrentSublistValue({
    sublistId: 'item',
    fieldId: 'amount',
    value: totalAmount
});
if (item.itemmemo) {
         invoice.setCurrentSublistValue({
         sublistId: 'item',
         fieldId: 'custcol_line_item_memo',
        value: item.itemmemo
        });
         }
         if (item.chassisId) {
        invoice.setCurrentSublistValue({
      sublistId: 'item',
        fieldId: 'custcol_chassis_id',
         value: item.chassisId
              });
            }
                       // Set location if provided
                       if (cost_center_id) {
                           var set_location = invoice.setCurrentSublistValue({
                               sublistId: 'item',
                               fieldId: 'location',
                               value: cost_center_id
                           });
                           log.debug("set_location", set_location);
                       }
                       if (item.itemmemo) {
                           invoice.setCurrentSublistValue({
                               sublistId: 'item',
                               fieldId: 'custcol_line_item_memo',
                               value: item.itemmemo
                           });
                       }
                       // Set location if provided
                       if (cost_center_id) {
                           var set_location = invoice.setCurrentSublistValue({
                               sublistId: 'item',
                               fieldId: 'location',
                               value: cost_center_id
                           });
                           log.debug("set_location", set_location);
                       }
                       invoice.commitLine({
                           sublistId: 'item'
                       });
                   });
               }
               // Save the invoice record
               var invoiceId = invoice.save({
                   enableSourcing: true,
                   ignoreMandatoryFields: true
               });
               log.debug("newly created invoiceId", invoiceId);
			   // Send a success email to the user
var currentUser = runtime.getCurrentUser();
var userEmail = currentUser.email; // Get the current user's email address
email.send({
	author: 1221733, // System user
	recipients: userEmail,
	subject: '---Invoice Created Successfully---',
	body: 'Your invoice has been created successfully. Invoice ID: ' + invoiceId + '. Invoice Number: ' + data.invoiceNumber
});

			   //TOLL Rebil invoices    
			       }else if(invoice_type_rec_id=='7'){
            var customformId=127;
            // if(customform=='125'){  
               var invoice = record.create({
                   type: record.Type.INVOICE,
                   isDynamic: true
               });
               log.debug("New invoice created", invoice);
   
   
               invoice.setValue({
                   fieldId: 'customform',
                   value: customformId // Set the form to the specified form
               });
   
               // Set the customer field
               if (data.invoiceNumber) {
                   var invoiceNo=invoice.setValue({
                       fieldId: 'tranid',
                       value: data.invoiceNumber // Ensure the customer ID exists in NetSuite
                   });
               }
               if (customer_internal_id) {
                   var set_cust = invoice.setValue({
                       fieldId: 'entity',
                       value: customer_internal_id // Ensure the customer ID exists in NetSuite
                   });
               }
               log.debug("Customer set", set_cust);
               if (data.invoiceDate) {
                   invoice.setValue({
                       fieldId: 'trandate',
                       value: data.invoiceDate
                   });
               }
               if (data.dueDate) {
                   invoice.setValue({
                       fieldId: 'duedate',
                       value: data.dueDate
                   });
               }
               if (data.sourceappcode) {
                   invoice.setValue({
                       fieldId: 'custbody_source_application_code',
                       value: data.sourceappcode
                   });
               }
               if (data.memoHeader) {
                   invoice.setValue({
                       fieldId: 'memo',
                       value: data.memoHeader
                   });
               }
                if(invoice_type_rec_id){
                invoice.setValue({
                    fieldId:'custbody_invoice_type',
                    value:invoice_type_rec_id
                });
             }
                // Add items to the invoice
               if (data.items && Array.isArray(data.items) && data.items.length > 0) {
                   data.items.forEach(function(item, index) {
                       // Validate item
                       if (!item.item_cd || !item.quantity || !item.rate) {
                           throw error.create({
                               name: 'INVALID_ITEM',
                               message: 'Item name, quantity, and rate are required for item at index ' + index
                           });
                       }
                       var itemInternalId = getItemDetails(item.item_cd);
                       if (!itemInternalId) {
                           throw error.create({
                               name: 'INVALID_ITEM',
                               message: 'Item not found: ' + item.item_cd
                           });
                       }
                       try {
                           var cost_center_id = getLocation(item.costCenter);
                           if (!cost_center_id) {
                               log.debug("invalid cost_center_id", +cost_center_id);
                           }
                       } catch (e) {
                           log.error("Error in cost_center_ id call", e.message);
                       }
                       invoice.selectNewLine({
                           sublistId: 'item'
                       });
                       // Set item details
                       //  itemInternalId.forEach(function(itemInternalId) {
                       invoice.setCurrentSublistValue({
                           sublistId: 'item',
                           fieldId: 'item',
                           value: itemInternalId
                       });
                       //  });
                       invoice.setCurrentSublistValue({
                           sublistId: 'item',
                           fieldId: 'quantity',
                           value: item.quantity
                       });
                       invoice.setCurrentSublistValue({
                           sublistId: 'item',
                           fieldId: 'rate',
                           value: item.rate
                       });
                      // Convert charge and tax to numeric values (default to 0 if missing)
var chargeAmount = parseFloat(item.charge_amt) || 0;
var taxAmount = parseFloat(item.tax_amount) || 0;

// Set custom charge amount field
if (chargeAmount !== 0) {
    invoice.setCurrentSublistValue({
        sublistId: 'item',
        fieldId: 'custcol_charge_amount',
        value: chargeAmount
    });
}
// Set tax amount field - replace 'cust_tax_field' with your actual Field ID
if (taxAmount !== 0) {
    invoice.setCurrentSublistValue({
        sublistId: 'item',
        fieldId: 'custcol_tax_amount',
        value: taxAmount
    });
}
// Set total amount (charge + tax)
var totalAmount = chargeAmount + taxAmount;
invoice.setCurrentSublistValue({
    sublistId: 'item',
    fieldId: 'amount',
    value: totalAmount
});
if (item.itemmemo) {
         invoice.setCurrentSublistValue({
         sublistId: 'item',
         fieldId: 'custcol_line_item_memo',
        value: item.itemmemo
        });
         }
         if (item.chassisId) {
        invoice.setCurrentSublistValue({
      sublistId: 'item',
        fieldId: 'custcol_chassis_id',
         value: item.chassisId
              });
            }
                       // Set location if provided
                       if (cost_center_id) {
                           var set_location = invoice.setCurrentSublistValue({
                               sublistId: 'item',
                               fieldId: 'location',
                               value: cost_center_id
                           });
                           log.debug("set_location", set_location);
                       }
                       if (item.itemmemo) {
                           invoice.setCurrentSublistValue({
                               sublistId: 'item',
                               fieldId: 'custcol_line_item_memo',
                               value: item.itemmemo
                           });
                       }
                       // Set location if provided
                       if (cost_center_id) {
                           var set_location = invoice.setCurrentSublistValue({
                               sublistId: 'item',
                               fieldId: 'location',
                               value: cost_center_id
                           });
                           log.debug("set_location", set_location);
                       }
                       invoice.commitLine({
                           sublistId: 'item'
                       });
                   });
               }
               // Save the invoice record
               var invoiceId = invoice.save({
                   enableSourcing: true,
                   ignoreMandatoryFields: true
               });
               log.debug("newly created invoiceId", invoiceId);
			   // Send a success email to the user
var currentUser = runtime.getCurrentUser();
var userEmail = currentUser.email; // Get the current user's email address
email.send({
	author: 1221733, // System user
	recipients: userEmail,
	subject: '---Invoice Created Successfully---',
	body: 'Your invoice has been created successfully. Invoice ID: ' + invoiceId + '. Invoice Number: ' + data.invoiceNumber
});

			   //Invoice Type-- Toll Citation VeraMobility    
			       }else if(invoice_type_rec_id=='8'){
            var customformId=127;
            // if(customform=='125'){  
               var invoice = record.create({
                   type: record.Type.INVOICE,
                   isDynamic: true
               });
               log.debug("New invoice created", invoice);
   
   
               invoice.setValue({
                   fieldId: 'customform',
                   value: customformId // Set the form to the specified form
               });
   
               // Set the customer field
               if (data.invoiceNumber) {
                   var invoiceNo=invoice.setValue({
                       fieldId: 'tranid',
                       value: data.invoiceNumber // Ensure the customer ID exists in NetSuite
                   });
               }
               if (customer_internal_id) {
                   var set_cust = invoice.setValue({
                       fieldId: 'entity',
                       value: customer_internal_id // Ensure the customer ID exists in NetSuite
                   });
               }
               log.debug("Customer set", set_cust);
               if (data.invoiceDate) {
                   invoice.setValue({
                       fieldId: 'trandate',
                       value: data.invoiceDate
                   });
               }
               if (data.dueDate) {
                   invoice.setValue({
                       fieldId: 'duedate',
                       value: data.dueDate
                   });
               }
               if (data.sourceappcode) {
                   invoice.setValue({
                       fieldId: 'custbody_source_application_code',
                       value: data.sourceappcode
                   });
               }
               if (data.memoHeader) {
                   invoice.setValue({
                       fieldId: 'memo',
                       value: data.memoHeader
                   });
               }
               if(invoice_type_rec_id){
                invoice.setValue({
                    fieldId:'custbody_invoice_type',
                    value:invoice_type_rec_id
                });
             }
                // Add items to the invoice
               if (data.items && Array.isArray(data.items) && data.items.length > 0) {
                   data.items.forEach(function(item, index) {
                       // Validate item
                       if (!item.item_cd || !item.quantity || !item.rate) {
                           throw error.create({
                               name: 'INVALID_ITEM',
                               message: 'Item name, quantity, and rate are required for item at index ' + index
                           });
                       }
                       var itemInternalId = getItemDetails(item.item_cd);
                       if (!itemInternalId) {
                           throw error.create({
                               name: 'INVALID_ITEM',
                               message: 'Item not found: ' + item.item_cd
                           });
                       }
                       try {
                           var cost_center_id = getLocation(item.costCenter);
                           if (!cost_center_id) {
                               log.debug("invalid cost_center_id", +cost_center_id);
                           }
                       } catch (e) {
                           log.error("Error in cost_center_ id call", e.message);
                       }
                       invoice.selectNewLine({
                           sublistId: 'item'
                       });
                       // Set item details
                       //  itemInternalId.forEach(function(itemInternalId) {
                       invoice.setCurrentSublistValue({
                           sublistId: 'item',
                           fieldId: 'item',
                           value: itemInternalId
                       });
                       //  });
                       invoice.setCurrentSublistValue({
                           sublistId: 'item',
                           fieldId: 'quantity',
                           value: item.quantity
                       });
                       invoice.setCurrentSublistValue({
                           sublistId: 'item',
                           fieldId: 'rate',
                           value: item.rate
                       });
                      // Convert charge and tax to numeric values (default to 0 if missing)
var chargeAmount = parseFloat(item.charge_amt) || 0;
var taxAmount = parseFloat(item.tax_amount) || 0;

// Set custom charge amount field
if (chargeAmount !== 0) {
    invoice.setCurrentSublistValue({
        sublistId: 'item',
        fieldId: 'custcol_charge_amount',
        value: chargeAmount
    });
}
// Set tax amount field - replace 'cust_tax_field' with your actual Field ID
if (taxAmount !== 0) {
    invoice.setCurrentSublistValue({
        sublistId: 'item',
        fieldId: 'custcol_tax_amount',
        value: taxAmount
    });
}
// Set total amount (charge + tax)
var totalAmount = chargeAmount + taxAmount;
invoice.setCurrentSublistValue({
    sublistId: 'item',
    fieldId: 'amount',
    value: totalAmount
});
if (item.itemmemo) {
         invoice.setCurrentSublistValue({
         sublistId: 'item',
         fieldId: 'custcol_line_item_memo',
        value: item.itemmemo
        });
         }
         if (item.chassisId) {
        invoice.setCurrentSublistValue({
      sublistId: 'item',
        fieldId: 'custcol_chassis_id',
         value: item.chassisId
              });
            }
                       // Set location if provided
                       if (cost_center_id) {
                           var set_location = invoice.setCurrentSublistValue({
                               sublistId: 'item',
                               fieldId: 'location',
                               value: cost_center_id
                           });
                           log.debug("set_location", set_location);
                       }
                       if (item.itemmemo) {
                           invoice.setCurrentSublistValue({
                               sublistId: 'item',
                               fieldId: 'custcol_line_item_memo',
                               value: item.itemmemo
                           });
                       }
                       // Set location if provided
                       if (cost_center_id) {
                           var set_location = invoice.setCurrentSublistValue({
                               sublistId: 'item',
                               fieldId: 'location',
                               value: cost_center_id
                           });
                           log.debug("set_location", set_location);
                       }
                       invoice.commitLine({
                           sublistId: 'item'
                       });
                   });
               }
               // Save the invoice record
               var invoiceId = invoice.save({
                   enableSourcing: true,
                   ignoreMandatoryFields: true
               });
               log.debug("newly created invoiceId", invoiceId);
			   // Send a success email to the user
var currentUser = runtime.getCurrentUser();
var userEmail = currentUser.email; // Get the current user's email address
email.send({
	author: 1221733, // System user
	recipients: userEmail,
	subject: '---Invoice Created Successfully---',
	body: 'Your invoice has been created successfully. Invoice ID: ' + invoiceId + '. Invoice Number: ' + data.invoiceNumber
});

			   //Invoice Tyupe ---MNR Invoice    
			       }else if(invoice_type_rec_id=='9'){
            var customformId=127;
            // if(customform=='125'){  
               var invoice = record.create({
                   type: record.Type.INVOICE,
                   isDynamic: true
               });
               log.debug("New invoice created", invoice);
   
   
               invoice.setValue({
                   fieldId: 'customform',
                   value: customformId // Set the form to the specified form
               });
   
               // Set the customer field
               if (data.invoiceNumber) {
                   var invoiceNo=invoice.setValue({
                       fieldId: 'tranid',
                       value: data.invoiceNumber // Ensure the customer ID exists in NetSuite
                   });
               }
               if (customer_internal_id) {
                   var set_cust = invoice.setValue({
                       fieldId: 'entity',
                       value: customer_internal_id // Ensure the customer ID exists in NetSuite
                   });
               }
               log.debug("Customer set", set_cust);
               if (data.invoiceDate) {
                   invoice.setValue({
                       fieldId: 'trandate',
                       value: data.invoiceDate
                   });
               }
               if (data.dueDate) {
                   invoice.setValue({
                       fieldId: 'duedate',
                       value: data.dueDate
                   });
               }
               if (data.sourceappcode) {
                   invoice.setValue({
                       fieldId: 'custbody_source_application_code',
                       value: data.sourceappcode
                   });
               }
               if (data.memoHeader) {
                   invoice.setValue({
                       fieldId: 'memo',
                       value: data.memoHeader
                   });
               }
               if(invoice_type_rec_id){
                invoice.setValue({
                    fieldId:'custbody_invoice_type',
                    value:invoice_type_rec_id
                });
             }
     // Add items to the invoice
               if (data.items && Array.isArray(data.items) && data.items.length > 0) {
                   data.items.forEach(function(item, index) {
                       // Validate item
                       if (!item.item_cd || !item.quantity || !item.rate) {
                           throw error.create({
                               name: 'INVALID_ITEM',
                               message: 'Item name, quantity, and rate are required for item at index ' + index
                           });
                       }
                       var itemInternalId = getItemDetails(item.item_cd);
                       if (!itemInternalId) {
                           throw error.create({
                               name: 'INVALID_ITEM',
                               message: 'Item not found: ' + item.item_cd
                           });
                       }
                       try {
                           var cost_center_id = getLocation(item.costCenter);
                           if (!cost_center_id) {
                               log.debug("invalid cost_center_id", +cost_center_id);
                           }
                       } catch (e) {
                           log.error("Error in cost_center_ id call", e.message);
                       }
                       invoice.selectNewLine({
                           sublistId: 'item'
                       });
                       // Set item details
                       //  itemInternalId.forEach(function(itemInternalId) {
                       invoice.setCurrentSublistValue({
                           sublistId: 'item',
                           fieldId: 'item',
                           value: itemInternalId
                       });
                       //  });
                       invoice.setCurrentSublistValue({
                           sublistId: 'item',
                           fieldId: 'quantity',
                           value: item.quantity
                       });
                       invoice.setCurrentSublistValue({
                           sublistId: 'item',
                           fieldId: 'rate',
                           value: item.rate
                       });
                      // Convert charge and tax to numeric values (default to 0 if missing)
var chargeAmount = parseFloat(item.charge_amt) || 0;
var taxAmount = parseFloat(item.tax_amount) || 0;

// Set custom charge amount field
if (chargeAmount !== 0) {
    invoice.setCurrentSublistValue({
        sublistId: 'item',
        fieldId: 'custcol_charge_amount',
        value: chargeAmount
    });
}
// Set tax amount field - replace 'cust_tax_field' with your actual Field ID
if (taxAmount !== 0) {
    invoice.setCurrentSublistValue({
        sublistId: 'item',
        fieldId: 'custcol_tax_amount',
        value: taxAmount
    });
}
// Set total amount (charge + tax)
var totalAmount = chargeAmount + taxAmount;
invoice.setCurrentSublistValue({
    sublistId: 'item',
    fieldId: 'amount',
    value: totalAmount
});
if (item.itemmemo) {
         invoice.setCurrentSublistValue({
         sublistId: 'item',
         fieldId: 'custcol_line_item_memo',
        value: item.itemmemo
        });
         }
         if (item.chassisId) {
        invoice.setCurrentSublistValue({
      sublistId: 'item',
        fieldId: 'custcol_chassis_id',
         value: item.chassisId
              });
            }
                       // Set location if provided
                       if (cost_center_id) {
                           var set_location = invoice.setCurrentSublistValue({
                               sublistId: 'item',
                               fieldId: 'location',
                               value: cost_center_id
                           });
                           log.debug("set_location", set_location);
                       }
                       if (item.itemmemo) {
                           invoice.setCurrentSublistValue({
                               sublistId: 'item',
                               fieldId: 'custcol_line_item_memo',
                               value: item.itemmemo
                           });
                       }
                       // Set location if provided
                       if (cost_center_id) {
                           var set_location = invoice.setCurrentSublistValue({
                               sublistId: 'item',
                               fieldId: 'location',
                               value: cost_center_id
                           });
                           log.debug("set_location", set_location);
                       }
                       invoice.commitLine({
                           sublistId: 'item'
                       });
                   });
               }
               // Save the invoice record
               var invoiceId = invoice.save({
                   enableSourcing: true,
                   ignoreMandatoryFields: true
               });
               log.debug("newly created invoiceId", invoiceId); 
			   // Send a success email to the user
var currentUser = runtime.getCurrentUser();
var userEmail = currentUser.email; // Get the current user's email address
email.send({
	author: 1221733, // System user
	recipients: userEmail,
	subject: '---Invoice Created Successfully---',
	body: 'Your invoice has been created successfully. Invoice ID: ' + invoiceId + '. Invoice Number: ' + data.invoiceNumber
});

			   //Invoice Type --MnR Rebil   
			       }else if(invoice_type_rec_id=='10'){
            var customformId=127;
            // if(customform=='125'){  
               var invoice = record.create({
                   type: record.Type.INVOICE,
                   isDynamic: true
               });
               log.debug("New invoice created", invoice);
   
   
               invoice.setValue({
                   fieldId: 'customform',
                   value: customformId // Set the form to the specified form
               });
   
               // Set the customer field
               if (data.invoiceNumber) {
                   var invoiceNo=invoice.setValue({
                       fieldId: 'tranid',
                       value: data.invoiceNumber // Ensure the customer ID exists in NetSuite
                   });
               }
               if (customer_internal_id) {
                   var set_cust = invoice.setValue({
                       fieldId: 'entity',
                       value: customer_internal_id // Ensure the customer ID exists in NetSuite
                   });
               }
               log.debug("Customer set", set_cust);
               if (data.invoiceDate) {
                   invoice.setValue({
                       fieldId: 'trandate',
                       value: data.invoiceDate
                   });
               }
               if (data.dueDate) {
                   invoice.setValue({
                       fieldId: 'duedate',
                       value: data.dueDate
                   });
               }
               if (data.sourceappcode) {
                   invoice.setValue({
                       fieldId: 'custbody_source_application_code',
                       value: data.sourceappcode
                   });
               }
               if (data.memoHeader) {
                   invoice.setValue({
                       fieldId: 'memo',
                       value: data.memoHeader
                   });
               }
               if(invoice_type_rec_id){
                invoice.setValue({
                    fieldId:'custbody_invoice_type',
                    value:invoice_type_rec_id
                });
             }
                   // Add items to the invoice
               if (data.items && Array.isArray(data.items) && data.items.length > 0) {
                   data.items.forEach(function(item, index) {
                       // Validate item
                       if (!item.item_cd || !item.quantity || !item.rate) {
                           throw error.create({
                               name: 'INVALID_ITEM',
                               message: 'Item name, quantity, and rate are required for item at index ' + index
                           });
                       }
                       var itemInternalId = getItemDetails(item.item_cd);
                       if (!itemInternalId) {
                           throw error.create({
                               name: 'INVALID_ITEM',
                               message: 'Item not found: ' + item.item_cd
                           });
                       }
                       try {
                           var cost_center_id = getLocation(item.costCenter);
                           if (!cost_center_id) {
                               log.debug("invalid cost_center_id", +cost_center_id);
                           }
                       } catch (e) {
                           log.error("Error in cost_center_ id call", e.message);
                       }
                       invoice.selectNewLine({
                           sublistId: 'item'
                       });
                       // Set item details
                       //  itemInternalId.forEach(function(itemInternalId) {
                       invoice.setCurrentSublistValue({
                           sublistId: 'item',
                           fieldId: 'item',
                           value: itemInternalId
                       });
                       //  });
                       invoice.setCurrentSublistValue({
                           sublistId: 'item',
                           fieldId: 'quantity',
                           value: item.quantity
                       });
                       invoice.setCurrentSublistValue({
                           sublistId: 'item',
                           fieldId: 'rate',
                           value: item.rate
                       });
                      // Convert charge and tax to numeric values (default to 0 if missing)
var chargeAmount = parseFloat(item.charge_amt) || 0;
var taxAmount = parseFloat(item.tax_amount) || 0;

// Set custom charge amount field
if (chargeAmount !== 0) {
    invoice.setCurrentSublistValue({
        sublistId: 'item',
        fieldId: 'custcol_charge_amount',
        value: chargeAmount
    });
}
// Set tax amount field - replace 'cust_tax_field' with your actual Field ID
if (taxAmount !== 0) {
    invoice.setCurrentSublistValue({
        sublistId: 'item',
        fieldId: 'custcol_tax_amount',
        value: taxAmount
    });
}
// Set total amount (charge + tax)
var totalAmount = chargeAmount + taxAmount;
invoice.setCurrentSublistValue({
    sublistId: 'item',
    fieldId: 'amount',
    value: totalAmount
});
if (item.itemmemo) {
         invoice.setCurrentSublistValue({
         sublistId: 'item',
         fieldId: 'custcol_line_item_memo',
        value: item.itemmemo
        });
         }
         if (item.chassisId) {
        invoice.setCurrentSublistValue({
      sublistId: 'item',
        fieldId: 'custcol_chassis_id',
         value: item.chassisId
              });
            }
                       // Set location if provided
                       if (cost_center_id) {
                           var set_location = invoice.setCurrentSublistValue({
                               sublistId: 'item',
                               fieldId: 'location',
                               value: cost_center_id
                           });
                           log.debug("set_location", set_location);
                       }
                       if (item.itemmemo) {
                           invoice.setCurrentSublistValue({
                               sublistId: 'item',
                               fieldId: 'custcol_line_item_memo',
                               value: item.itemmemo
                           });
                       }
                       // Set location if provided
                       if (cost_center_id) {
                           var set_location = invoice.setCurrentSublistValue({
                               sublistId: 'item',
                               fieldId: 'location',
                               value: cost_center_id
                           });
                           log.debug("set_location", set_location);
                       }
                       invoice.commitLine({
                           sublistId: 'item'
                       });
                   });
               }
               // Save the invoice record
               var invoiceId = invoice.save({
                   enableSourcing: true,
                   ignoreMandatoryFields: true
               });
               log.debug("newly created invoiceId", invoiceId);
			   // Send a success email to the user
var currentUser = runtime.getCurrentUser();
var userEmail = currentUser.email; // Get the current user's email address
email.send({
	author: 1221733, // System user
	recipients: userEmail,
	subject: '---Invoice Created Successfully---',
	body: 'Your invoice has been created successfully. Invoice ID: ' + invoiceId + '. Invoice Number: ' + data.invoiceNumber
});

			   //Invoice Type:--POOL Invoices   
			       }else if(invoice_type_rec_id=='11'){
            var customformId=127;
            // if(customform=='125'){  
               var invoice = record.create({
                   type: record.Type.INVOICE,
                   isDynamic: true
               });
               log.debug("New invoice created", invoice);
   
   
               invoice.setValue({
                   fieldId: 'customform',
                   value: customformId // Set the form to the specified form
               });
   
               // Set the customer field
               if (data.invoiceNumber) {
                   var invoiceNo=invoice.setValue({
                       fieldId: 'tranid',
                       value: data.invoiceNumber // Ensure the customer ID exists in NetSuite
                   });
               }
               if (customer_internal_id) {
                   var set_cust = invoice.setValue({
                       fieldId: 'entity',
                       value: customer_internal_id // Ensure the customer ID exists in NetSuite
                   });
               }
               log.debug("Customer set", set_cust);
               if (data.invoiceDate) {
                   invoice.setValue({
                       fieldId: 'trandate',
                       value: data.invoiceDate
                   });
               }
               if (data.dueDate) {
                   invoice.setValue({
                       fieldId: 'duedate',
                       value: data.dueDate
                   });
               }
               if (data.sourceappcode) {
                   invoice.setValue({
                       fieldId: 'custbody_source_application_code',
                       value: data.sourceappcode
                   });
               }
               if (data.memoHeader) {
                   invoice.setValue({
                       fieldId: 'memo',
                       value: data.memoHeader
                   });
               }
                 if(invoice_type_rec_id){
                invoice.setValue({
                    fieldId:'custbody_invoice_type',
                    value:invoice_type_rec_id
                });
             }
     // Add items to the invoice
               if (data.items && Array.isArray(data.items) && data.items.length > 0) {
                   data.items.forEach(function(item, index) {
                       // Validate item
                       if (!item.item_cd || !item.quantity || !item.rate) {
                           throw error.create({
                               name: 'INVALID_ITEM',
                               message: 'Item name, quantity, and rate are required for item at index ' + index
                           });
                       }
                       var itemInternalId = getItemDetails(item.item_cd);
                       if (!itemInternalId) {
                           throw error.create({
                               name: 'INVALID_ITEM',
                               message: 'Item not found: ' + item.item_cd
                           });
                       }
                       try {
                           var cost_center_id = getLocation(item.costCenter);
                           if (!cost_center_id) {
                               log.debug("invalid cost_center_id", +cost_center_id);
                           }
                       } catch (e) {
                           log.error("Error in cost_center_ id call", e.message);
                       }
                       invoice.selectNewLine({
                           sublistId: 'item'
                       });
                       // Set item details
                       //  itemInternalId.forEach(function(itemInternalId) {
                       invoice.setCurrentSublistValue({
                           sublistId: 'item',
                           fieldId: 'item',
                           value: itemInternalId
                       });
                       //  });
                       invoice.setCurrentSublistValue({
                           sublistId: 'item',
                           fieldId: 'quantity',
                           value: item.quantity
                       });
                       invoice.setCurrentSublistValue({
                           sublistId: 'item',
                           fieldId: 'rate',
                           value: item.rate
                       });
                      // Convert charge and tax to numeric values (default to 0 if missing)
var chargeAmount = parseFloat(item.charge_amt) || 0;
var taxAmount = parseFloat(item.tax_amount) || 0;

// Set custom charge amount field
if (chargeAmount !== 0) {
    invoice.setCurrentSublistValue({
        sublistId: 'item',
        fieldId: 'custcol_charge_amount',
        value: chargeAmount
    });
}
// Set tax amount field - replace 'cust_tax_field' with your actual Field ID
if (taxAmount !== 0) {
    invoice.setCurrentSublistValue({
        sublistId: 'item',
        fieldId: 'custcol_tax_amount',
        value: taxAmount
    });
}
// Set total amount (charge + tax)
var totalAmount = chargeAmount + taxAmount;
invoice.setCurrentSublistValue({
    sublistId: 'item',
    fieldId: 'amount',
    value: totalAmount
});
if (item.itemmemo) {
         invoice.setCurrentSublistValue({
         sublistId: 'item',
         fieldId: 'custcol_line_item_memo',
        value: item.itemmemo
        });
         }
         if (item.chassisId) {
        invoice.setCurrentSublistValue({
      sublistId: 'item',
        fieldId: 'custcol_chassis_id',
         value: item.chassisId
              });
            }
                       // Set location if provided
                       if (cost_center_id) {
                           var set_location = invoice.setCurrentSublistValue({
                               sublistId: 'item',
                               fieldId: 'location',
                               value: cost_center_id
                           });
                           log.debug("set_location", set_location);
                       }
                       if (item.itemmemo) {
                           invoice.setCurrentSublistValue({
                               sublistId: 'item',
                               fieldId: 'custcol_line_item_memo',
                               value: item.itemmemo
                           });
                       }
                       // Set location if provided
                       if (cost_center_id) {
                           var set_location = invoice.setCurrentSublistValue({
                               sublistId: 'item',
                               fieldId: 'location',
                               value: cost_center_id
                           });
                           log.debug("set_location", set_location);
                       }
                       invoice.commitLine({
                           sublistId: 'item'
                       });
                   });
               }
               // Save the invoice record
               var invoiceId = invoice.save({
                   enableSourcing: true,
                   ignoreMandatoryFields: true
               });
               log.debug("newly created invoiceId", invoiceId);
			   // Send a success email to the user
var currentUser = runtime.getCurrentUser();
var userEmail = currentUser.email; // Get the current user's email address
email.send({
	author: 1221733, // System user
	recipients: userEmail,
	subject: '---Invoice Created Successfully---',
	body: 'Your invoice has been created successfully. Invoice ID: ' + invoiceId + '. Invoice Number: ' + data.invoiceNumber
});

			   //Invoice Type -- POOL Rebil Invoice    
			       }else if(invoice_type_rec_id=='12'){
            var customformId=127;
            // if(customform=='125'){  
               var invoice = record.create({
                   type: record.Type.INVOICE,
                   isDynamic: true
               });
               log.debug("New invoice created", invoice);
   
   
               invoice.setValue({
                   fieldId: 'customform',
                   value: customformId // Set the form to the specified form
               });
   
               // Set the customer field
               if (data.invoiceNumber) {
                   var invoiceNo=invoice.setValue({
                       fieldId: 'tranid',
                       value: data.invoiceNumber // Ensure the customer ID exists in NetSuite
                   });
               }
               if (customer_internal_id) {
                   var set_cust = invoice.setValue({
                       fieldId: 'entity',
                       value: customer_internal_id // Ensure the customer ID exists in NetSuite
                   });
               }
               log.debug("Customer set", set_cust);
               if (data.invoiceDate) {
                   invoice.setValue({
                       fieldId: 'trandate',
                       value: data.invoiceDate
                   });
               }
               if (data.dueDate) {
                   invoice.setValue({
                       fieldId: 'duedate',
                       value: data.dueDate
                   });
               }
               if (data.sourceappcode) {
                   invoice.setValue({
                       fieldId: 'custbody_source_application_code',
                       value: data.sourceappcode
                   });
               }
               if (data.memoHeader) {
                   invoice.setValue({
                       fieldId: 'memo',
                       value: data.memoHeader
                   });
               }
                if(invoice_type_rec_id){
                invoice.setValue({
                    fieldId:'custbody_invoice_type',
                    value:invoice_type_rec_id
                });
             }
     // Add items to the invoice
               if (data.items && Array.isArray(data.items) && data.items.length > 0) {
                   data.items.forEach(function(item, index) {
                       // Validate item
                       if (!item.item_cd || !item.quantity || !item.rate) {
                           throw error.create({
                               name: 'INVALID_ITEM',
                               message: 'Item name, quantity, and rate are required for item at index ' + index
                           });
                       }
                       var itemInternalId = getItemDetails(item.item_cd);
                       if (!itemInternalId) {
                           throw error.create({
                               name: 'INVALID_ITEM',
                               message: 'Item not found: ' + item.item_cd
                           });
                       }
                       try {
                           var cost_center_id = getLocation(item.costCenter);
                           if (!cost_center_id) {
                               log.debug("invalid cost_center_id", +cost_center_id);
                           }
                       } catch (e) {
                           log.error("Error in cost_center_ id call", e.message);
                       }
                       invoice.selectNewLine({
                           sublistId: 'item'
                       });
                       // Set item details
                       //  itemInternalId.forEach(function(itemInternalId) {
                       invoice.setCurrentSublistValue({
                           sublistId: 'item',
                           fieldId: 'item',
                           value: itemInternalId
                       });
                       //  });
                       invoice.setCurrentSublistValue({
                           sublistId: 'item',
                           fieldId: 'quantity',
                           value: item.quantity
                       });
                       invoice.setCurrentSublistValue({
                           sublistId: 'item',
                           fieldId: 'rate',
                           value: item.rate
                       });
                      // Convert charge and tax to numeric values (default to 0 if missing)
var chargeAmount = parseFloat(item.charge_amt) || 0;
var taxAmount = parseFloat(item.tax_amount) || 0;

// Set custom charge amount field
if (chargeAmount !== 0) {
    invoice.setCurrentSublistValue({
        sublistId: 'item',
        fieldId: 'custcol_charge_amount',
        value: chargeAmount
    });
}
// Set tax amount field - replace 'cust_tax_field' with your actual Field ID
if (taxAmount !== 0) {
    invoice.setCurrentSublistValue({
        sublistId: 'item',
        fieldId: 'custcol_tax_amount',
        value: taxAmount
    });
}
// Set total amount (charge + tax)
var totalAmount = chargeAmount + taxAmount;
invoice.setCurrentSublistValue({
    sublistId: 'item',
    fieldId: 'amount',
    value: totalAmount
});
if (item.itemmemo) {
         invoice.setCurrentSublistValue({
         sublistId: 'item',
         fieldId: 'custcol_line_item_memo',
        value: item.itemmemo
        });
         }
         if (item.chassisId) {
        invoice.setCurrentSublistValue({
      sublistId: 'item',
        fieldId: 'custcol_chassis_id',
         value: item.chassisId
              });
            }
                       // Set location if provided
                       if (cost_center_id) {
                           var set_location = invoice.setCurrentSublistValue({
                               sublistId: 'item',
                               fieldId: 'location',
                               value: cost_center_id
                           });
                           log.debug("set_location", set_location);
                       }
                       if (item.itemmemo) {
                           invoice.setCurrentSublistValue({
                               sublistId: 'item',
                               fieldId: 'custcol_line_item_memo',
                               value: item.itemmemo
                           });
                       }
                       // Set location if provided
                       if (cost_center_id) {
                           var set_location = invoice.setCurrentSublistValue({
                               sublistId: 'item',
                               fieldId: 'location',
                               value: cost_center_id
                           });
                           log.debug("set_location", set_location);
                       }
                       invoice.commitLine({
                           sublistId: 'item'
                       });
                   });
               }
               // Save the invoice record
               var invoiceId = invoice.save({
                   enableSourcing: true,
                   ignoreMandatoryFields: true
               });
               log.debug("newly created invoiceId", invoiceId); 
			   // Send a success email to the user
var currentUser = runtime.getCurrentUser();
var userEmail = currentUser.email; // Get the current user's email address
email.send({
	author: 1221733, // System user
	recipients: userEmail,
	subject: '---Invoice Created Successfully---',
	body: 'Your invoice has been created successfully. Invoice ID: ' + invoiceId + '. Invoice Number: ' + data.invoiceNumber
});

			   //Invoice Type---    DMW credit memeo invoice
			       }else if(invoice_type_rec_id=='13'){
            var customformId=127;
            // if(customform=='125'){  
               var invoice = record.create({
                   type: record.Type.INVOICE,
                   isDynamic: true
               });
               log.debug("New invoice created", invoice);
   
   
               invoice.setValue({
                   fieldId: 'customform',
                   value: customformId // Set the form to the specified form
               });
   
               // Set the customer field
               if (data.invoiceNumber) {
                   var invoiceNo=invoice.setValue({
                       fieldId: 'tranid',
                       value: data.invoiceNumber // Ensure the customer ID exists in NetSuite
                   });
               }
               if (customer_internal_id) {
                   var set_cust = invoice.setValue({
                       fieldId: 'entity',
                       value: customer_internal_id // Ensure the customer ID exists in NetSuite
                   });
               }
               log.debug("Customer set", set_cust);
               if (data.invoiceDate) {
                   invoice.setValue({
                       fieldId: 'trandate',
                       value: data.invoiceDate
                   });
               }
               if (data.dueDate) {
                   invoice.setValue({
                       fieldId: 'duedate',
                       value: data.dueDate
                   });
               }
               if (data.sourceappcode) {
                   invoice.setValue({
                       fieldId: 'custbody_source_application_code',
                       value: data.sourceappcode
                   });
               }
               if (data.memoHeader) {
                   invoice.setValue({
                       fieldId: 'memo',
                       value: data.memoHeader
                   });
               }
                if(data.invoice_type){
                   invoice.setValue({
                       fieldId:'custbody_invoice_type',
                       value:data.invoice_type
                   });
                }
     // Add items to the invoice
               if (data.items && Array.isArray(data.items) && data.items.length > 0) {
                   data.items.forEach(function(item, index) {
                       // Validate item
                       if (!item.item_cd || !item.quantity || !item.rate) {
                           throw error.create({
                               name: 'INVALID_ITEM',
                               message: 'Item name, quantity, and rate are required for item at index ' + index
                           });
                       }
                       var itemInternalId = getItemDetails(item.item_cd);
                       if (!itemInternalId) {
                           throw error.create({
                               name: 'INVALID_ITEM',
                               message: 'Item not found: ' + item.item_cd
                           });
                       }
                       try {
                           var cost_center_id = getLocation(item.costCenter);
                           if (!cost_center_id) {
                               log.debug("invalid cost_center_id", +cost_center_id);
                           }
                       } catch (e) {
                           log.error("Error in cost_center_ id call", e.message);
                       }
                       invoice.selectNewLine({
                           sublistId: 'item'
                       });
                       // Set item details
                       //  itemInternalId.forEach(function(itemInternalId) {
                       invoice.setCurrentSublistValue({
                           sublistId: 'item',
                           fieldId: 'item',
                           value: itemInternalId
                       });
                       //  });
                       invoice.setCurrentSublistValue({
                           sublistId: 'item',
                           fieldId: 'quantity',
                           value: item.quantity
                       });
                       invoice.setCurrentSublistValue({
                           sublistId: 'item',
                           fieldId: 'rate',
                           value: item.rate
                       });
                      // Convert charge and tax to numeric values (default to 0 if missing)
var chargeAmount = parseFloat(item.charge_amt) || 0;
var taxAmount = parseFloat(item.tax_amount) || 0;

// Set custom charge amount field
if (chargeAmount !== 0) {
    invoice.setCurrentSublistValue({
        sublistId: 'item',
        fieldId: 'custcol_charge_amount',
        value: chargeAmount
    });
}
// Set tax amount field - replace 'cust_tax_field' with your actual Field ID
if (taxAmount !== 0) {
    invoice.setCurrentSublistValue({
        sublistId: 'item',
        fieldId: 'custcol_tax_amount',
        value: taxAmount
    });
}
// Set total amount (charge + tax)
var totalAmount = chargeAmount + taxAmount;
invoice.setCurrentSublistValue({
    sublistId: 'item',
    fieldId: 'amount',
    value: totalAmount
});
if (item.itemmemo) {
         invoice.setCurrentSublistValue({
         sublistId: 'item',
         fieldId: 'custcol_line_item_memo',
        value: item.itemmemo
        });
         }
         if (item.chassisId) {
        invoice.setCurrentSublistValue({
      sublistId: 'item',
        fieldId: 'custcol_chassis_id',
         value: item.chassisId
              });
            }
                       // Set location if provided
                       if (cost_center_id) {
                           var set_location = invoice.setCurrentSublistValue({
                               sublistId: 'item',
                               fieldId: 'location',
                               value: cost_center_id
                           });
                           log.debug("set_location", set_location);
                       }
                       if (item.itemmemo) {
                           invoice.setCurrentSublistValue({
                               sublistId: 'item',
                               fieldId: 'custcol_line_item_memo',
                               value: item.itemmemo
                           });
                       }
                       // Set location if provided
                       if (cost_center_id) {
                           var set_location = invoice.setCurrentSublistValue({
                               sublistId: 'item',
                               fieldId: 'location',
                               value: cost_center_id
                           });
                           log.debug("set_location", set_location);
                       }
                       invoice.commitLine({
                           sublistId: 'item'
                       });
                   });
               }
               // Save the invoice record
               var invoiceId = invoice.save({
                   enableSourcing: true,
                   ignoreMandatoryFields: true
               });
               log.debug("newly created invoiceId", invoiceId);
			   // Send a success email to the user
var currentUser = runtime.getCurrentUser();
var userEmail = currentUser.email; // Get the current user's email address
email.send({
	author: 1221733, // System user
	recipients: userEmail,
	subject: '---Invoice Created Successfully---',
	body: 'Your invoice has been created successfully. Invoice ID: ' + invoiceId + '. Invoice Number: ' + data.invoiceNumber
});

			   //Invoice Type ---Tow and Impound    
			       }else if(invoice_type_rec_id=='14'){
            var customformId=127;
            // if(customform=='125'){  
               var invoice = record.create({
                   type: record.Type.INVOICE,
                   isDynamic: true
               });
               log.debug("New invoice created", invoice);
   
   
               invoice.setValue({
                   fieldId: 'customform',
                   value: customformId // Set the form to the specified form
               });
   
               // Set the customer field
               if (data.invoiceNumber) {
                   var invoiceNo=invoice.setValue({
                       fieldId: 'tranid',
                       value: data.invoiceNumber // Ensure the customer ID exists in NetSuite
                   });
               }
               if (customer_internal_id) {
                   var set_cust = invoice.setValue({
                       fieldId: 'entity',
                       value: customer_internal_id // Ensure the customer ID exists in NetSuite
                   });
               }
               log.debug("Customer set", set_cust);
               if (data.invoiceDate) {
                   invoice.setValue({
                       fieldId: 'trandate',
                       value: data.invoiceDate
                   });
               }
               if (data.dueDate) {
                   invoice.setValue({
                       fieldId: 'duedate',
                       value: data.dueDate
                   });
               }
               if (data.sourceappcode) {
                   invoice.setValue({
                       fieldId: 'custbody_source_application_code',
                       value: data.sourceappcode
                   });
               }
               if (data.memoHeader) {
                   invoice.setValue({
                       fieldId: 'memo',
                       value: data.memoHeader
                   });
               }
                if(invoice_type_rec_id){
                invoice.setValue({
                    fieldId:'custbody_invoice_type',
                    value:invoice_type_rec_id
                });
             }
                 // Add items to the invoice
               if (data.items && Array.isArray(data.items) && data.items.length > 0) {
                   data.items.forEach(function(item, index) {
                       // Validate item
                       if (!item.item_cd || !item.quantity || !item.rate) {
                           throw error.create({
                               name: 'INVALID_ITEM',
                               message: 'Item name, quantity, and rate are required for item at index ' + index
                           });
                       }
                       var itemInternalId = getItemDetails(item.item_cd);
                       if (!itemInternalId) {
                           throw error.create({
                               name: 'INVALID_ITEM',
                               message: 'Item not found: ' + item.item_cd
                           });
                       }
                       try {
                           var cost_center_id = getLocation(item.costCenter);
                           if (!cost_center_id) {
                               log.debug("invalid cost_center_id", +cost_center_id);
                           }
                       } catch (e) {
                           log.error("Error in cost_center_ id call", e.message);
                       }
                       invoice.selectNewLine({
                           sublistId: 'item'
                       });
                       // Set item details
                       //  itemInternalId.forEach(function(itemInternalId) {
                       invoice.setCurrentSublistValue({
                           sublistId: 'item',
                           fieldId: 'item',
                           value: itemInternalId
                       });
                       //  });
                       invoice.setCurrentSublistValue({
                           sublistId: 'item',
                           fieldId: 'quantity',
                           value: item.quantity
                       });
                       invoice.setCurrentSublistValue({
                           sublistId: 'item',
                           fieldId: 'rate',
                           value: item.rate
                       });
                      // Convert charge and tax to numeric values (default to 0 if missing)
var chargeAmount = parseFloat(item.charge_amt) || 0;
var taxAmount = parseFloat(item.tax_amount) || 0;

// Set custom charge amount field
if (chargeAmount !== 0) {
    invoice.setCurrentSublistValue({
        sublistId: 'item',
        fieldId: 'custcol_charge_amount',
        value: chargeAmount
    });
}
// Set tax amount field - replace 'cust_tax_field' with your actual Field ID
if (taxAmount !== 0) {
    invoice.setCurrentSublistValue({
        sublistId: 'item',
        fieldId: 'custcol_tax_amount',
        value: taxAmount
    });
}
// Set total amount (charge + tax)
var totalAmount = chargeAmount + taxAmount;
invoice.setCurrentSublistValue({
    sublistId: 'item',
    fieldId: 'amount',
    value: totalAmount
});
if (item.itemmemo) {
         invoice.setCurrentSublistValue({
         sublistId: 'item',
         fieldId: 'custcol_line_item_memo',
        value: item.itemmemo
        });
         }
         if (item.chassisId) {
        invoice.setCurrentSublistValue({
      sublistId: 'item',
        fieldId: 'custcol_chassis_id',
         value: item.chassisId
              });
            }
                       // Set location if provided
                       if (cost_center_id) {
                           var set_location = invoice.setCurrentSublistValue({
                               sublistId: 'item',
                               fieldId: 'location',
                               value: cost_center_id
                           });
                           log.debug("set_location", set_location);
                       }
                       if (item.itemmemo) {
                           invoice.setCurrentSublistValue({
                               sublistId: 'item',
                               fieldId: 'custcol_line_item_memo',
                               value: item.itemmemo
                           });
                       }
                       // Set location if provided
                       if (cost_center_id) {
                           var set_location = invoice.setCurrentSublistValue({
                               sublistId: 'item',
                               fieldId: 'location',
                               value: cost_center_id
                           });
                           log.debug("set_location", set_location);
                       }
                       invoice.commitLine({
                           sublistId: 'item'
                       });
                   });
               }
               // Save the invoice record
               var invoiceId = invoice.save({
                   enableSourcing: true,
                   ignoreMandatoryFields: true
               });
               log.debug("newly created invoiceId", invoiceId); 
			   // Send a success email to the user
var currentUser = runtime.getCurrentUser();
var userEmail = currentUser.email; // Get the current user's email address
email.send({
	author: 1221733, // System user
	recipients: userEmail,
	subject: '---Invoice Created Successfully---',
	body: 'Your invoice has been created successfully. Invoice ID: ' + invoiceId + '. Invoice Number: ' + data.invoiceNumber
});

			   //Invoice Type ---   Reposition Rebil Invoice
			       }else if(invoice_type_rec_id=='15'){
            var customformId=127;
            // if(customform=='125'){  
               var invoice = record.create({
                   type: record.Type.INVOICE,
                   isDynamic: true
               });
               log.debug("New invoice created", invoice);
   
   
               invoice.setValue({
                   fieldId: 'customform',
                   value: customformId // Set the form to the specified form
               });
   
               // Set the customer field
               if (data.invoiceNumber) {
                   var invoiceNo=invoice.setValue({
                       fieldId: 'tranid',
                       value: data.invoiceNumber // Ensure the customer ID exists in NetSuite
                   });
               }
               if (customer_internal_id) {
                   var set_cust = invoice.setValue({
                       fieldId: 'entity',
                       value: customer_internal_id // Ensure the customer ID exists in NetSuite
                   });
               }
               log.debug("Customer set", set_cust);
               if (data.invoiceDate) {
                   invoice.setValue({
                       fieldId: 'trandate',
                       value: data.invoiceDate
                   });
               }
               if (data.dueDate) {
                   invoice.setValue({
                       fieldId: 'duedate',
                       value: data.dueDate
                   });
               }
               if (data.sourceappcode) {
                   invoice.setValue({
                       fieldId: 'custbody_source_application_code',
                       value: data.sourceappcode
                   });
               }
               if (data.memoHeader) {
                   invoice.setValue({
                       fieldId: 'memo',
                       value: data.memoHeader
                   });
               }
                if(invoice_type_rec_id){
                invoice.setValue({
                    fieldId:'custbody_invoice_type',
                    value:invoice_type_rec_id
                });
             }
     // Add items to the invoice
               if (data.items && Array.isArray(data.items) && data.items.length > 0) {
                   data.items.forEach(function(item, index) {
                       // Validate item
                       if (!item.item_cd || !item.quantity || !item.rate) {
                           throw error.create({
                               name: 'INVALID_ITEM',
                               message: 'Item name, quantity, and rate are required for item at index ' + index
                           });
                       }
                       var itemInternalId = getItemDetails(item.item_cd);
                       if (!itemInternalId) {
                           throw error.create({
                               name: 'INVALID_ITEM',
                               message: 'Item not found: ' + item.item_cd
                           });
                       }
                       try {
                           var cost_center_id = getLocation(item.costCenter);
                           if (!cost_center_id) {
                               log.debug("invalid cost_center_id", +cost_center_id);
                           }
                       } catch (e) {
                           log.error("Error in cost_center_ id call", e.message);
                       }
                       invoice.selectNewLine({
                           sublistId: 'item'
                       });
                       // Set item details
                       //  itemInternalId.forEach(function(itemInternalId) {
                       invoice.setCurrentSublistValue({
                           sublistId: 'item',
                           fieldId: 'item',
                           value: itemInternalId
                       });
                       //  });
                       invoice.setCurrentSublistValue({
                           sublistId: 'item',
                           fieldId: 'quantity',
                           value: item.quantity
                       });
                       invoice.setCurrentSublistValue({
                           sublistId: 'item',
                           fieldId: 'rate',
                           value: item.rate
                       });
                      // Convert charge and tax to numeric values (default to 0 if missing)
var chargeAmount = parseFloat(item.charge_amt) || 0;
var taxAmount = parseFloat(item.tax_amount) || 0;

// Set custom charge amount field
if (chargeAmount !== 0) {
    invoice.setCurrentSublistValue({
        sublistId: 'item',
        fieldId: 'custcol_charge_amount',
        value: chargeAmount
    });
}
// Set tax amount field - replace 'cust_tax_field' with your actual Field ID
if (taxAmount !== 0) {
    invoice.setCurrentSublistValue({
        sublistId: 'item',
        fieldId: 'custcol_tax_amount',
        value: taxAmount
    });
}
// Set total amount (charge + tax)
var totalAmount = chargeAmount + taxAmount;
invoice.setCurrentSublistValue({
    sublistId: 'item',
    fieldId: 'amount',
    value: totalAmount
});
if (item.itemmemo) {
         invoice.setCurrentSublistValue({
         sublistId: 'item',
         fieldId: 'custcol_line_item_memo',
        value: item.itemmemo
        });
         }
         if (item.chassisId) {
        invoice.setCurrentSublistValue({
      sublistId: 'item',
        fieldId: 'custcol_chassis_id',
         value: item.chassisId
              });
            }
                       // Set location if provided
                       if (cost_center_id) {
                           var set_location = invoice.setCurrentSublistValue({
                               sublistId: 'item',
                               fieldId: 'location',
                               value: cost_center_id
                           });
                           log.debug("set_location", set_location);
                       }
                       if (item.itemmemo) {
                           invoice.setCurrentSublistValue({
                               sublistId: 'item',
                               fieldId: 'custcol_line_item_memo',
                               value: item.itemmemo
                           });
                       }
                       // Set location if provided
                       if (cost_center_id) {
                           var set_location = invoice.setCurrentSublistValue({
                               sublistId: 'item',
                               fieldId: 'location',
                               value: cost_center_id
                           });
                           log.debug("set_location", set_location);
                       }
                       invoice.commitLine({
                           sublistId: 'item'
                       });
                   });
               }
               // Save the invoice record
               var invoiceId = invoice.save({
                   enableSourcing: true,
                   ignoreMandatoryFields: true
               });
               log.debug("newly created invoiceId", invoiceId);  
			   // Send a success email to the user
var currentUser = runtime.getCurrentUser();
var userEmail = currentUser.email; // Get the current user's email address
email.send({
	author: 1221733, // System user
	recipients: userEmail,
	subject: '---Invoice Created Successfully---',
	body: 'Your invoice has been created successfully. Invoice ID: ' + invoiceId + '. Invoice Number: ' + data.invoiceNumber
});

			   //Invoice Type--    Miscellaneous invoice  
			       }else if(invoice_type_rec_id=='16'){
            var customformId=127;
            // if(customform=='125'){  
               var invoice = record.create({
                   type: record.Type.INVOICE,
                   isDynamic: true
               });
               log.debug("New invoice created", invoice);
   
   
               invoice.setValue({
                   fieldId: 'customform',
                   value: customformId // Set the form to the specified form
               });
   
               // Set the customer field
               if (data.invoiceNumber) {
                   var invoiceNo=invoice.setValue({
                       fieldId: 'tranid',
                       value: data.invoiceNumber // Ensure the customer ID exists in NetSuite
                   });
               }
               if (customer_internal_id) {
                   var set_cust = invoice.setValue({
                       fieldId: 'entity',
                       value: customer_internal_id // Ensure the customer ID exists in NetSuite
                   });
               }
               log.debug("Customer set", set_cust);
   
        
               if (data.invoiceDate) {
                   invoice.setValue({
                       fieldId: 'trandate',
                       value: data.invoiceDate
                   });
               }
               if (data.dueDate) {
                   invoice.setValue({
                       fieldId: 'duedate',
                       value: data.dueDate
                 });
               }
               if (data.sourceappcode) {
                   invoice.setValue({
                       fieldId: 'custbody_source_application_code',
                       value: data.sourceappcode
                   });
               }
               if (data.memoHeader) {
                   invoice.setValue({
                       fieldId: 'memo',
                       value: data.memoHeader
                   });
               }
              if(invoice_type_rec_id){
                invoice.setValue({
                    fieldId:'custbody_invoice_type',
                    value:invoice_type_rec_id
                });
             }
                  // Add items to the invoice
               if (data.items && Array.isArray(data.items) && data.items.length > 0) {
                   data.items.forEach(function(item, index) {
                       // Validate item
                       if (!item.item_cd || !item.quantity || !item.rate) {
                           throw error.create({
                               name: 'INVALID_ITEM',
                               message: 'Item name, quantity, and rate are required for item at index ' + index
                           });
                       }
                       var itemInternalId = getItemDetails(item.item_cd);
                       if (!itemInternalId) {
                           throw error.create({
                               name: 'INVALID_ITEM',
                               message: 'Item not found: ' + item.item_cd
                           });
                       }
                       try {
                           var cost_center_id = getLocation(item.costCenter);
                           if (!cost_center_id) {
                               log.debug("invalid cost_center_id", +cost_center_id);
                           }
                       } catch (e) {
                           log.error("Error in cost_center_ id call", e.message);
                       }
                       invoice.selectNewLine({
                           sublistId: 'item'
                       });
                       // Set item details
                       //  itemInternalId.forEach(function(itemInternalId) {
                       invoice.setCurrentSublistValue({
                           sublistId: 'item',
                           fieldId: 'item',
                           value: itemInternalId
                       });
                       //  });
                       invoice.setCurrentSublistValue({
                           sublistId: 'item',
                           fieldId: 'quantity',
                           value: item.quantity
                       });
                       invoice.setCurrentSublistValue({
                           sublistId: 'item',
                           fieldId: 'rate',
                           value: item.rate
                       });
                      // Convert charge and tax to numeric values (default to 0 if missing)
var chargeAmount = parseFloat(item.charge_amt) || 0;
var taxAmount = parseFloat(item.tax_amount) || 0;

// Set custom charge amount field
if (chargeAmount !== 0) {
    invoice.setCurrentSublistValue({
        sublistId: 'item',
        fieldId: 'custcol_charge_amount',
        value: chargeAmount
    });
}
// Set tax amount field - replace 'cust_tax_field' with your actual Field ID
if (taxAmount !== 0) {
    invoice.setCurrentSublistValue({
        sublistId: 'item',
        fieldId: 'custcol_tax_amount',
        value: taxAmount
    });
}
// Set total amount (charge + tax)
var totalAmount = chargeAmount + taxAmount;
invoice.setCurrentSublistValue({
    sublistId: 'item',
    fieldId: 'amount',
    value: totalAmount
});
if (item.itemmemo) {
         invoice.setCurrentSublistValue({
         sublistId: 'item',
         fieldId: 'custcol_line_item_memo',
        value: item.itemmemo
        });
         }
         if (item.chassisId) {
        invoice.setCurrentSublistValue({
      sublistId: 'item',
        fieldId: 'custcol_chassis_id',
         value: item.chassisId
              });
            }
                       // Set location if provided
                       if (cost_center_id) {
                           var set_location = invoice.setCurrentSublistValue({
                               sublistId: 'item',
                               fieldId: 'location',
                               value: cost_center_id
                           });
                           log.debug("set_location", set_location);
                       }
                       if (item.itemmemo) {
                           invoice.setCurrentSublistValue({
                               sublistId: 'item',
                               fieldId: 'custcol_line_item_memo',
                               value: item.itemmemo
                           });
                       }
                       // Set location if provided
                       if (cost_center_id) {
                           var set_location = invoice.setCurrentSublistValue({
                               sublistId: 'item',
                               fieldId: 'location',
                               value: cost_center_id
                           });
                           log.debug("set_location", set_location);
                       }
                       invoice.commitLine({
                           sublistId: 'item'
                       });
                   });
               }
               // Save the invoice record
               var invoiceId = invoice.save({
                   enableSourcing: true,
                   ignoreMandatoryFields: true
               });
               log.debug("newly created invoiceId", invoiceId);   
			   // Send a success email to the user
var currentUser = runtime.getCurrentUser();
var userEmail = currentUser.email; // Get the current user's email address
email.send({
	author: 1221733, // System user
	recipients: userEmail,
	subject: '---Invoice Created Successfully---',
	body: 'Your invoice has been created successfully. Invoice ID: ' + invoiceId + '. Invoice Number: ' + data.invoiceNumber
});
 // Invoice Type -- Supplemental Billing
		}else if(invoice_type_rec_id=='17'){
            var customformId=127;
            // if(customform=='125'){  
               var invoice = record.create({
                   type: record.Type.INVOICE,
                   isDynamic: true
               });
               log.debug("New invoice created", invoice);
   
   
               invoice.setValue({
                   fieldId: 'customform',
                   value: customformId // Set the form to the specified form
               });
   
               // Set the customer field
               if (data.invoiceNumber) {
                   var invoiceNo=invoice.setValue({
                       fieldId: 'tranid',
                       value: data.invoiceNumber // Ensure the customer ID exists in NetSuite
                   });
               }
               if (customer_internal_id) {
                   var set_cust = invoice.setValue({
                       fieldId: 'entity',
                       value: customer_internal_id // Ensure the customer ID exists in NetSuite
                   });
               }
               log.debug("Customer set", set_cust);
               if (data.invoiceDate) {
                   invoice.setValue({
                       fieldId: 'trandate',
                       value: data.invoiceDate
                   });
               }
               if (data.dueDate) {
                   invoice.setValue({
                       fieldId: 'duedate',
                       value: data.dueDate
                   });
               }
               if (data.sourceappcode) {
                   invoice.setValue({
                       fieldId: 'custbody_source_application_code',
                       value: data.sourceappcode
                   });
               }
               if (data.memoHeader) {
                   invoice.setValue({
                       fieldId: 'memo',
                       value: data.memoHeader
                   });
               }
              if(invoice_type_rec_id){
                invoice.setValue({
                    fieldId:'custbody_invoice_type',
                    value:invoice_type_rec_id
                });
             }
                   // Add items to the invoice
               if (data.items && Array.isArray(data.items) && data.items.length > 0) {
                   data.items.forEach(function(item, index) {
                       // Validate item
                       if (!item.item_cd || !item.quantity || !item.rate) {
                           throw error.create({
                               name: 'INVALID_ITEM',
                               message: 'Item name, quantity, and rate are required for item at index ' + index
                           });
                       }
                       var itemInternalId = getItemDetails(item.item_cd);
                       if (!itemInternalId) {
                           throw error.create({
                               name: 'INVALID_ITEM',
                               message: 'Item not found: ' + item.item_cd
                           });
                       }
                       try {
                           var cost_center_id = getLocation(item.costCenter);
                           if (!cost_center_id) {
                               log.debug("invalid cost_center_id", +cost_center_id);
                           }
                       } catch (e) {
                           log.error("Error in cost_center_ id call", e.message);
                       }
                       invoice.selectNewLine({
                           sublistId: 'item'
                       });
                       // Set item details
                       //  itemInternalId.forEach(function(itemInternalId) {
                       invoice.setCurrentSublistValue({
                           sublistId: 'item',
                           fieldId: 'item',
                           value: itemInternalId
                       });
                       //  });
                       invoice.setCurrentSublistValue({
                           sublistId: 'item',
                           fieldId: 'quantity',
                           value: item.quantity
                       });
                       invoice.setCurrentSublistValue({
                           sublistId: 'item',
                           fieldId: 'rate',
                           value: item.rate
                       });
                      // Convert charge and tax to numeric values (default to 0 if missing)
var chargeAmount = parseFloat(item.charge_amt) || 0;
var taxAmount = parseFloat(item.tax_amount) || 0;

// Set custom charge amount field
if (chargeAmount !== 0) {
    invoice.setCurrentSublistValue({
        sublistId: 'item',
        fieldId: 'custcol_charge_amount',
        value: chargeAmount
    });
}
// Set tax amount field - replace 'cust_tax_field' with your actual Field ID
if (taxAmount !== 0) {
    invoice.setCurrentSublistValue({
        sublistId: 'item',
        fieldId: 'custcol_tax_amount',
        value: taxAmount
    });
}
// Set total amount (charge + tax)
var totalAmount = chargeAmount + taxAmount;
invoice.setCurrentSublistValue({
    sublistId: 'item',
    fieldId: 'amount',
    value: totalAmount
});
if (item.itemmemo) {
         invoice.setCurrentSublistValue({
         sublistId: 'item',
         fieldId: 'custcol_line_item_memo',
        value: item.itemmemo
        });
         }
         if (item.chassisId) {
        invoice.setCurrentSublistValue({
      sublistId: 'item',
        fieldId: 'custcol_chassis_id',
         value: item.chassisId
              });
            }
                       // Set location if provided
                       if (cost_center_id) {
                           var set_location = invoice.setCurrentSublistValue({
                               sublistId: 'item',
                               fieldId: 'location',
                               value: cost_center_id
                           });
                           log.debug("set_location", set_location);
                       }
                       if (item.itemmemo) {
                           invoice.setCurrentSublistValue({
                               sublistId: 'item',
                               fieldId: 'custcol_line_item_memo',
                               value: item.itemmemo
                           });
                       }
                       // Set location if provided
                       if (cost_center_id) {
                           var set_location = invoice.setCurrentSublistValue({
                               sublistId: 'item',
                               fieldId: 'location',
                               value: cost_center_id
                           });
                           log.debug("set_location", set_location);
                       }
                       invoice.commitLine({
                           sublistId: 'item'
                       });
                   });
               }
               // Save the invoice record
               var invoiceId = invoice.save({
                   enableSourcing: true,
                   ignoreMandatoryFields: true
               });
               log.debug("newly created invoiceId", invoiceId);   
			   // Send a success email to the user
var currentUser = runtime.getCurrentUser();
var userEmail = currentUser.email; // Get the current user's email address
email.send({
	author: 1221733, // System user
	recipients: userEmail,
	subject: '---Invoice Created Successfully---',
	body: 'Your invoice has been created successfully. Invoice ID: ' + invoiceId + '. Invoice Number: ' + data.invoiceNumber
});
 //  Invoice Type Miscellaneous invoice  
  }else if(invoice_type_rec_id=='18'){
            var customformId=127;
            // if(customform=='125'){  
               var invoice = record.create({
                   type: record.Type.INVOICE,
                   isDynamic: true
               });
               log.debug("New invoice created", invoice);
   
   
               invoice.setValue({
                   fieldId: 'customform',
                   value: customformId // Set the form to the specified form
               });
   
               // Set the customer field
               if (data.invoiceNumber) {
                   var invoiceNo=invoice.setValue({
                       fieldId: 'tranid',
                       value: data.invoiceNumber // Ensure the customer ID exists in NetSuite
                   });
               }
               if (customer_internal_id) {
                   var set_cust = invoice.setValue({
                       fieldId: 'entity',
                       value: customer_internal_id // Ensure the customer ID exists in NetSuite
                   });
               }
               log.debug("Customer set", set_cust);
               if (data.invoiceDate) {
                   invoice.setValue({
                       fieldId: 'trandate',
                       value: data.invoiceDate
                   });
               }
               if (data.dueDate) {
                   invoice.setValue({
                       fieldId: 'duedate',
                       value: data.dueDate
                   });
               }
               if (data.sourceappcode) {
                   invoice.setValue({
                       fieldId: 'custbody_source_application_code',
                       value: data.sourceappcode
                   });
               }
               if (data.memoHeader) {
                   invoice.setValue({
                       fieldId: 'memo',
                       value: data.memoHeader
                   });
               }
              if(invoice_type_rec_id){
                invoice.setValue({
                    fieldId:'custbody_invoice_type',
                    value:invoice_type_rec_id
                });
             }
                  // Add items to the invoice
               if (data.items && Array.isArray(data.items) && data.items.length > 0) {
                   data.items.forEach(function(item, index) {
                       // Validate item
                       if (!item.item_cd || !item.quantity || !item.rate) {
                           throw error.create({
                               name: 'INVALID_ITEM',
                               message: 'Item name, quantity, and rate are required for item at index ' + index
                           });
                       }
                       var itemInternalId = getItemDetails(item.item_cd);
                       if (!itemInternalId) {
                           throw error.create({
                               name: 'INVALID_ITEM',
                               message: 'Item not found: ' + item.item_cd
                           });
                       }
                       try {
                           var cost_center_id = getLocation(item.costCenter);
                           if (!cost_center_id) {
                               log.debug("invalid cost_center_id", +cost_center_id);
                           }
                       } catch (e) {
                           log.error("Error in cost_center_ id call", e.message);
                       }
                       invoice.selectNewLine({
                           sublistId: 'item'
                       });
                       // Set item details
                       //  itemInternalId.forEach(function(itemInternalId) {
                       invoice.setCurrentSublistValue({
                           sublistId: 'item',
                           fieldId: 'item',
                           value: itemInternalId
                       });
                       //  });
                       invoice.setCurrentSublistValue({
                           sublistId: 'item',
                           fieldId: 'quantity',
                           value: item.quantity
                       });
                       invoice.setCurrentSublistValue({
                           sublistId: 'item',
                           fieldId: 'rate',
                           value: item.rate
                       });
                      // Convert charge and tax to numeric values (default to 0 if missing)
var chargeAmount = parseFloat(item.charge_amt) || 0;
var taxAmount = parseFloat(item.tax_amount) || 0;

// Set custom charge amount field
if (chargeAmount !== 0) {
    invoice.setCurrentSublistValue({
        sublistId: 'item',
        fieldId: 'custcol_charge_amount',
        value: chargeAmount
    });
}
// Set tax amount field - replace 'cust_tax_field' with your actual Field ID
if (taxAmount !== 0) {
    invoice.setCurrentSublistValue({
        sublistId: 'item',
        fieldId: 'custcol_tax_amount',
        value: taxAmount
    });
}
// Set total amount (charge + tax)
var totalAmount = chargeAmount + taxAmount;
invoice.setCurrentSublistValue({
    sublistId: 'item',
    fieldId: 'amount',
    value: totalAmount
});
if (item.itemmemo) {
         invoice.setCurrentSublistValue({
         sublistId: 'item',
         fieldId: 'custcol_line_item_memo',
        value: item.itemmemo
        });
         }
         if (item.chassisId) {
        invoice.setCurrentSublistValue({
      sublistId: 'item',
        fieldId: 'custcol_chassis_id',
         value: item.chassisId
              });
            }
                       // Set location if provided
                       if (cost_center_id) {
                           var set_location = invoice.setCurrentSublistValue({
                               sublistId: 'item',
                               fieldId: 'location',
                               value: cost_center_id
                           });
                           log.debug("set_location", set_location);
                       }
                       if (item.itemmemo) {
                           invoice.setCurrentSublistValue({
                               sublistId: 'item',
                               fieldId: 'custcol_line_item_memo',
                               value: item.itemmemo
                           });
                       }
                       // Set location if provided
                       if (cost_center_id) {
                           var set_location = invoice.setCurrentSublistValue({
                               sublistId: 'item',
                               fieldId: 'location',
                               value: cost_center_id
                           });
                           log.debug("set_location", set_location);
                       }
                       invoice.commitLine({
                           sublistId: 'item'
                       });
                   });
               }
               // Save the invoice record
               var invoiceId = invoice.save({
                   enableSourcing: true,
                   ignoreMandatoryFields: true
               });
               log.debug("newly created invoiceId", invoiceId);   
			   // Send a success email to the user
var currentUser = runtime.getCurrentUser();
var userEmail = currentUser.email; // Get the current user's email address
email.send({
	author: 1221733, // System user
	recipients: userEmail,
	subject: '---Invoice Created Successfully---',
	body: 'Your invoice has been created successfully. Invoice ID: ' + invoiceId + '. Invoice Number: ' + data.invoiceNumber
});
 
 }else if(invoice_type_rec_id == '19'){
 var customformId = 120; // your credit memo custom form ID

    var creditMemo = record.create({
        type: record.Type.CREDIT_MEMO,
        isDynamic: true
    });
    log.debug("New Credit Memo created", creditMemo);

    creditMemo.setValue({
        fieldId: 'customform',
        value: customformId
    });

    // Set Customer
    if (customer_internal_id) {
        creditMemo.setValue({
            fieldId: 'entity',
            value: customer_internal_id
        });
    }

    // Basic header fields
    if (data.invoiceNumber) {
        creditMemo.setValue({
            fieldId: 'tranid',
            value: data.invoiceNumber
        });
    }
    if (data.invoiceDate) {
        creditMemo.setValue({
            fieldId: 'trandate',
            value: data.InvoiceDate
        });
    }
    if (data.dueDate) {
        creditMemo.setValue({
            fieldId: 'duedate',
            value: data.dueDate
        });
    }
    if (data.memoHeader) {
        creditMemo.setValue({
            fieldId: 'memo',
            value: data.memoHeader
        });
    }
    if (data.sourceappcode) {
        creditMemo.setValue({
            fieldId: 'custbody_source_application_code',
            value: data.sourceappcode
        });
    }

    // Custom Invoice Type (still store for reference)
    creditMemo.setValue({
        fieldId: 'custbody_invoice_type',
        value: invoice_type_rec_id
    });

    // Add line items
    if (data.items && Array.isArray(data.items) && data.items.length > 0) {
        data.items.forEach(function(item, index) {
            var itemInternalId = getItemDetails(item.item_cd);
            if (!itemInternalId) {
                throw error.create({
                    name: 'INVALID_ITEM',
                    message: 'Item not found: ' + item.item_cd
                });
            }

            // Optionally get cost center
            var cost_center_id;
            try {
                cost_center_id = getLocation(item.costCenter);
            } catch (e) {
                log.error("Error getting cost center", e.message);
            }

            creditMemo.selectNewLine({ sublistId: 'item' });
            creditMemo.setCurrentSublistValue({
                sublistId: 'item',
                fieldId: 'item',
                value: itemInternalId
            });
            creditMemo.setCurrentSublistValue({
                sublistId: 'item',
                fieldId: 'quantity',
                value: Math.abs(item.quantity) // ensure positive value
            });
            if (item.rate) {
                creditMemo.setCurrentSublistValue({
                    sublistId: 'item',
                    fieldId: 'rate',
                    value: item.rate
                });
            }

            //
     // Convert charge and tax to numeric values (default to 0 if missing)
var chargeAmount = parseFloat(item.charge_amt) || 0;
var taxAmount = parseFloat(item.tax_amount) || 0;
// Set custom charge amount field
if (chargeAmount !== 0) {
    creditMemo.setCurrentSublistValue({
        sublistId: 'item',
        fieldId: 'custcol_charge_amount',
        value: chargeAmount
    });
}
// Set tax amount field - replace 'cust_tax_field' with your actual Field ID
if (taxAmount !== 0) {
    creditMemo.setCurrentSublistValue({
        sublistId: 'item',
        fieldId: 'custcol_tax_amount',
        value: taxAmount
    });
}
// Set total amount (charge + tax)
var totalAmount = chargeAmount + taxAmount;
creditMemo.setCurrentSublistValue({
    sublistId: 'item',
    fieldId: 'amount',
    value: totalAmount
});
            if (item.itemmemo) {
                creditMemo.setCurrentSublistValue({
                    sublistId: 'item',
                    fieldId: 'custcol_line_item_memo',
                    value: item.itemmemo
                });
            }
            
            if (cost_center_id) {
                creditMemo.setCurrentSublistValue({
                    sublistId: 'item',
                    fieldId: 'location',
                    value: cost_center_id
                });
            }
           
            creditMemo.commitLine({ sublistId: 'item' });
        });
    }

    // Save Credit Memo
    var creditMemoId = creditMemo.save({
        enableSourcing: true,
        ignoreMandatoryFields: true
    });

    log.debug("New Credit Memo Created", creditMemoId);

    // Send success email
    var currentUser = runtime.getCurrentUser();
    email.send({
        author: currentUser.id,
        recipients: currentUser.email,
        subject: '---Credit Memo Created Successfully---',
        body: 'Your Credit Memo has been created successfully. ID: ' + creditMemoId + '. Reference Number: ' + data.invoiceNumber
    });
                   }else{
					return false;
				   }
///end of invoice
			return {
				success: true,
				invoiceId:invoiceId
			};
        //  }
		} catch (e) {
			log.error({
				title: 'Invoice Creation unsuccessfull',
				details: e.message
			});
			// Send an error email to the user
            var currentUser = runtime.getCurrentUser();
            var userEmail = currentUser.email; // Get the current user's email address
            email.send({
                author: 1221733, // System user
                recipients: userEmail,
                subject: 'Invoice Creation unsuccessfull due to Invalid Value----',
                body: 'Invoice can not create due missing value: ' + e.message
            });
			return {
				success: false,
				message: e.message
			};
		}
	}
	//load the customer aand get the customer internal ids
	function getCustomerDetail(customerId) {
		try {
			// var customer="10003963";
			//log.debug("Customer parameter received", customer);
			if (customerId) {
				var customerSearchObj = search.create({
					type: "customer",
					filters: [
						["entityid", "is", customerId]
					],
					columns: [
						search.createColumn({
							name: "internalid",
							label: "Internal ID"
						})
					]
				});
				log.debug("customerSearchObj", customerSearchObj);
				var searchResult = customerSearchObj.run().getRange({
					start: 0,
					end: 1
				});
				log.debug(" --customer searchResult", searchResult);
				if (searchResult.length > 0) {
					var customer_internal_id = searchResult[0].getValue({
						name: 'internalid'
					});
					log.debug("customer_internal_id", customer_internal_id);
					return customer_internal_id;
				} else {
					return null;
				}
			} else {
				return null;
			}
		} catch (e) {
			log.error("Error Fetching customer details", e.message);
			return null;
		}
	}
	//function to handle location for invoice creations
	function getLocation(costCenter) {
		try {
			log.debug("cost center function started", "Received costCenter: " + costCenter);
			if (!costCenter) {
				log.error("cost center function error", "No cost center name provided");
				return null;
			}
			if (costCenter) {
				var locationSearchObj = search.create({
					type: "location",
					filters: [
						["externalid", "is", costCenter]
					],
					columns: [
						search.createColumn({
							name: "internalid",
							label: "Internal ID"
						}),
						//  search.createColumn({name: "name", label: "Name"}),
						//  search.createColumn({name: "externalid", label: "External ID"}),
						//  search.createColumn({name: "cseg_location_code", label: "Location Code"})
					]
				});
				var locationSearchResults = locationSearchObj.run().getRange({
					start: 0,
					end: 1
				});
				log.debug("Location Search Results", locationSearchResults);
				if (locationSearchResults.length > 0) {
					var locationId = locationSearchResults[0].getValue({
						name: 'internalid'
					});
					log.debug("---Location found", locationId);
					return locationId;
				} else {
					log.error("Location" + location);
					return null;
				}
			} else {
				return null;
			}

		} catch (e) {
			log.error("Error Fetching cosr center Internal ID", e.message);
			return null;
		}
	}
	   	//function call retrive invoice type from custom invoice type record
	function getinvoice_type(invoice_type) {
		try {
			// var postingPeriod="Oct 2024";
			var customrecord_invoice_typeSearchObj = search.create({
              type: "customrecord_invoice_type",
                filters:
                [
                        ["name","contains",invoice_type]

                    //  ["name","startswith",invoice_type]
                   ],
				columns: [
			    search.createColumn({name: "internalid", label: "Internal ID"})
				]
			});
			var searchResult = customrecord_invoice_typeSearchObj.run().getRange({
				start: 0,
				end: 1
			});
			log.debug("-- Invoice Type record results", searchResult);
			if (searchResult.length > 0) {
				var invoice_type_id = searchResult[0].getValue({
					name: 'internalid'
				});
				log.debug("---Invoice Type internal ID", invoice_type_id);
				return invoice_type_id;
			} else {
				return null;
			}
		} catch (e) {
			log.error("Error Fetching invoice type custom record", e.message);
			return null;
		}
	}
	// Function to retrieve item details based on provided item names
	function getItemDetails(item_cd) {
		try {
			// Perform search to get item internal IDs
			if (item_cd) {
				var itemSearchObj = search.create({
					type: "item",
					filters: [
						["name", "is", item_cd]
					],
					columns: [
						search.createColumn({
							name: "internalid",
							label: "Internal ID"
						})
					]
				});
				var searchResult = itemSearchObj.run().getRange({
					start: 0,
					end: 1
				});
				if (searchResult.length > 0) {
					var itemInternalId = searchResult[0].getValue({
						name: 'internalid'
					});
					log.debug("Item internal ID found", itemInternalId);
					return itemInternalId;
				} else {
					log.error("Item not found", item_cd);
					return null;
				}
			} else {
				return null;
			}
		} catch (e) {
			log.error("Error Fetching Item Details", e.message);
			return null;
		}
	}
	return {
		post: createInvoice
	};
});