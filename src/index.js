import React from 'react';
import ReactDOM from 'react-dom';
import { Router } from "react-router-dom";
import history from "./History.js";
import './index.css';
import App from './App';

ReactDOM.render(
  <Router history={history} basename="/pk-webs">
    <App />
  </Router>,
  document.getElementById('root')
);
