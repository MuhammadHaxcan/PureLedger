import { useState, useEffect, useRef } from 'react';
import './SearchableSelect.css';

const SearchableSelect = ({
  value,
  onChange,
  options = [],
  placeholder = 'Search...',
  disabled = false,
  required = false,
  size = 'sm',
  onClear,
  className = ''
}) => {
  const [searchTerm, setSearchTerm] = useState('');
  const [isOpen, setIsOpen] = useState(false);
  const [selectedIndex, setSelectedIndex] = useState(-1);
  const containerRef = useRef(null);
  const inputRef = useRef(null);
  const dropdownRef = useRef(null);

  // Find selected option (handle null values for "All" options)
  // Use loose equality to handle type coercion (number vs string)
  const selectedOption = options.find(opt => {
    const optValue = opt.id !== undefined ? opt.id : opt.value;
    // Handle strict equality first
    if (optValue === value) return true;
    // Handle null/empty cases
    if ((optValue === null || optValue === '') && (value === null || value === '')) return true;
    // Handle type coercion for numbers (e.g., 5 === "5" should match)
    if (optValue != null && value != null) {
      const optNum = Number(optValue);
      const valNum = Number(value);
      if (!isNaN(optNum) && !isNaN(valNum) && optNum === valNum) return true;
    }
    return false;
  });
  const displayValue = selectedOption ? (selectedOption.name || selectedOption.label || selectedOption.text) : '';

  // Filter options based on search term
  const filteredOptions = options.filter(opt => {
    const text = (opt.name || opt.label || opt.text || '').toLowerCase();
    return text.includes(searchTerm.toLowerCase());
  });

  // Update search input when value changes externally
  useEffect(() => {
    if (selectedOption) {
      setSearchTerm(displayValue);
    } else if (!isOpen && value === null) {
      setSearchTerm('');
    } else if (!isOpen && value !== null && value !== undefined) {
      // If value exists but option not found, try to find it again
      const found = options.find(opt => {
        const optValue = opt.id !== undefined ? opt.id : opt.value;
        return optValue === value;
      });
      if (found) {
        setSearchTerm(found.name || found.label || found.text || '');
      } else {
        setSearchTerm('');
      }
    }
  }, [value, selectedOption, displayValue, isOpen, options]);

  // Handle click outside
  useEffect(() => {
    const handleClickOutside = (event) => {
      if (containerRef.current && !containerRef.current.contains(event.target)) {
        setIsOpen(false);
        if (!selectedOption) {
          setSearchTerm('');
        }
      }
    };

    document.addEventListener('mousedown', handleClickOutside);
    return () => document.removeEventListener('mousedown', handleClickOutside);
  }, [selectedOption]);

  // Scroll selected option into view
  useEffect(() => {
    if (isOpen && selectedIndex >= 0 && dropdownRef.current) {
      const selectedElement = dropdownRef.current.querySelector(`[data-index="${selectedIndex}"]`);
      if (selectedElement) {
        selectedElement.scrollIntoView({ block: 'nearest', behavior: 'smooth' });
      }
    }
  }, [selectedIndex, isOpen]);

  const handleInputChange = (e) => {
    const term = e.target.value;
    setSearchTerm(term);
    setIsOpen(true);
    setSelectedIndex(-1);

    // If input is cleared and onClear callback exists, call it
    if (term === '' && onClear) {
      onClear();
      // Delay clearing search term to allow parent to update value first
      setTimeout(() => {
        setSearchTerm('');
      }, 0);
    }
  };

  const handleInputFocus = () => {
    setIsOpen(true);
    // Highlight currently selected option
    if (selectedOption) {
      const index = filteredOptions.findIndex(opt => 
        (opt.id === value || opt.value === value)
      );
      if (index >= 0) {
        setSelectedIndex(index);
      }
    }
  };

  const handleKeyDown = (e) => {
    if (disabled) return;

    switch (e.key) {
      case 'ArrowDown':
        e.preventDefault();
        if (filteredOptions.length === 0) return;
        setIsOpen(true);
        setSelectedIndex(prev => {
          if (prev < filteredOptions.length - 1) {
            return prev + 1;
          }
          return prev;
        });
        break;

      case 'ArrowUp':
        e.preventDefault();
        if (filteredOptions.length === 0) return;
        setIsOpen(true);
        setSelectedIndex(prev => {
          if (prev > 0) {
            return prev - 1;
          }
          return 0;
        });
        break;

      case 'Enter':
        e.preventDefault();
        if (isOpen && selectedIndex >= 0 && filteredOptions[selectedIndex]) {
          const option = filteredOptions[selectedIndex];
          handleSelect(option);
        } else if (isOpen && filteredOptions.length > 0) {
          // Select first option if nothing is selected
          handleSelect(filteredOptions[0]);
        }
        break;

      case 'Escape':
        e.preventDefault();
        setIsOpen(false);
        if (!selectedOption) {
          setSearchTerm('');
        }
        break;

      case 'Tab':
        if (isOpen) {
          setIsOpen(false);
        }
        break;

      default:
        break;
    }
  };

  const handleSelect = (option) => {
    const optionValue = option.id !== undefined ? option.id : option.value;
    // Handle null/empty values for "All" options
    onChange(optionValue === null || optionValue === '' ? null : optionValue);
    setSearchTerm(option.name || option.label || option.text || '');
    setIsOpen(false);
    setSelectedIndex(-1);
  };

  const handleOptionClick = (option, index) => {
    handleSelect(option);
  };

  const handleOptionMouseEnter = (index) => {
    setSelectedIndex(index);
  };

  return (
    <div className={`searchable-select-container select-container ${className}`} ref={containerRef}>
      <input
        ref={inputRef}
        type="text"
        className={`form-control form-control-${size} select-search`}
        placeholder={placeholder}
        value={searchTerm}
        onChange={handleInputChange}
        onFocus={handleInputFocus}
        onKeyDown={handleKeyDown}
        disabled={disabled}
        autoComplete="off"
      />
      <select
        className="form-select form-select-sm category-select d-none"
        value={value !== null && value !== undefined ? value : ''}
        onChange={(e) => {
          const val = e.target.value;
          if (val === '' || val === 'null') {
            onChange(null);
          } else {
            onChange(isNaN(val) ? val : parseInt(val));
          }
        }}
        required={required}
        disabled={disabled}
        tabIndex={-1}
      >
        <option value="">{placeholder}</option>
        {options.map((opt, idx) => {
          const optValue = opt.id !== undefined ? opt.id : opt.value;
          const optText = opt.name || opt.label || opt.text;
          return (
            <option key={idx} value={optValue === null ? '' : optValue}>
              {optText}
            </option>
          );
        })}
      </select>
      <div
        ref={dropdownRef}
        className={`searchable-select-dropdown select-dropdown ${isOpen && !disabled ? 'show' : ''}`}
      >
        {filteredOptions.length === 0 ? (
          <div className="searchable-select-option select-option text-muted">No options found</div>
        ) : (
          filteredOptions.map((option, index) => {
            const optValue = option.id !== undefined ? option.id : option.value;
            const optText = option.name || option.label || option.text;
            const isSelected = (optValue === value) || (optValue === null && value === null) || (optValue === '' && (value === null || value === '')) || (selectedIndex === index);
            const isHighlighted = selectedIndex === index;
            
            return (
              <div
                key={index}
                data-index={index}
                data-value={optValue}
                className={`searchable-select-option select-option ${isSelected ? 'selected' : ''} ${isHighlighted ? 'highlighted' : ''}`}
                onClick={() => handleOptionClick(option, index)}
                onMouseEnter={() => handleOptionMouseEnter(index)}
              >
                {optText}
              </div>
            );
          })
        )}
      </div>
    </div>
  );
};

export default SearchableSelect;

